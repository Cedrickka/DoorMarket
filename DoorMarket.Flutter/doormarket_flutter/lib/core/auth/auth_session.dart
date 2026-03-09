import 'dart:convert';

import 'package:dio/dio.dart';

import '../api/auth_api.dart';
import '../models/auth.dart';
import '../storage/token_store.dart';

class AuthSession {
  AuthSession({
    required this.authApi,
    required this.tokenStore,
    required this.rawHttp,
  });

  final AuthApi authApi;
  final TokenStore tokenStore;
  final Dio rawHttp;

  Future<bool> initialize() async {
    final access = await tokenStore.getAccessToken();
    if (access == null || access.isEmpty) {
      return false;
    }

    final expired = await tokenStore.isAccessTokenExpired();
    if (!expired) {
      return await _ensureRoleMatches(access);
    }

    return await tryRefresh();
  }

  Future<void> signInWithLogin(LoginResult result) async {
    final auth = AuthResponse(
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
      expiresAtUtc: result.expiresAtUtc,
    );
    await signIn(auth);
  }

  Future<void> signIn(AuthResponse response) async {
    await tokenStore.setTokens(
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      expiresAtUtc: response.expiresAtUtc,
    );

    final ok = await _ensureRoleMatches(response.accessToken);
    if (!ok) {
      await tokenStore.clear();
    }
  }

  Future<bool> tryRefresh() async {
    try {
      final refresh = await tokenStore.getRefreshToken();
      if (refresh == null || refresh.isEmpty) {
        await tokenStore.clear();
        return false;
      }

      final response = await authApi.refresh(RefreshRequest(refreshToken: refresh));
      await tokenStore.setTokens(
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        expiresAtUtc: response.expiresAtUtc,
      );

      return await _ensureRoleMatches(response.accessToken);
    } catch (_) {
      await tokenStore.clear();
      return false;
    }
  }

  Future<void> signOut() async {
    final refresh = await tokenStore.getRefreshToken();
    if (refresh != null && refresh.isNotEmpty) {
      try {
        await authApi.logout(RefreshRequest(refreshToken: refresh));
      } catch (_) {}
    }

    await tokenStore.clear();
  }

  Future<bool> _ensureRoleMatches(String accessToken) async {
    final tokenRole = _readRoleFromToken(accessToken);
    if (tokenRole == null || tokenRole.isEmpty) {
      return true;
    }

    try {
      final me = await _fetchMe(accessToken);
      if (me == null) {
        return true;
      }
      final normalizedToken = _normalizeRole(tokenRole);
      final normalizedDb = _normalizeRole(me['role'] as String?);
      return normalizedToken.toLowerCase() == normalizedDb.toLowerCase();
    } catch (_) {
      // network error => do not block login
      return true;
    }
  }

  Future<Map<String, dynamic>?> _fetchMe(String accessToken) async {
    final response = await rawHttp.get(
      'api/me',
      options: Options(headers: {'Authorization': 'Bearer $accessToken'}),
    );
    if (response.statusCode == 401 || response.statusCode == 403) {
      return null;
    }
    final data = response.data;
    if (data is Map<String, dynamic>) {
      return data;
    }
    return null;
  }

  static String? _readRoleFromToken(String accessToken) {
    try {
      final parts = accessToken.split('.');
      if (parts.length < 2) return null;
      final json = _decodeBase64Url(parts[1]);
      if (json == null) return null;
      final data = jsonDecode(json);
      if (data is Map<String, dynamic>) {
        if (data['role'] is String) return data['role'] as String;
        for (final entry in data.entries) {
          if (entry.key.endsWith('/role') && entry.value is String) {
            return entry.value as String;
          }
        }
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  static String? _decodeBase64Url(String input) {
    var normalized = input.replaceAll('-', '+').replaceAll('_', '/');
    switch (normalized.length % 4) {
      case 2:
        normalized += '==';
        break;
      case 3:
        normalized += '=';
        break;
    }
    try {
      return utf8.decode(base64.decode(normalized));
    } catch (_) {
      return null;
    }
  }

  static String _normalizeRole(String? role) {
    final value = role?.trim() ?? '';
    if (value.isEmpty) return '';
    final parsed = int.tryParse(value);
    if (parsed == null) return value;
    switch (parsed) {
      case 1:
        return 'Client';
      case 2:
        return 'Shop';
      case 3:
        return 'Admin';
      case 4:
        return 'SuperAdmin';
      default:
        return value;
    }
  }
}
