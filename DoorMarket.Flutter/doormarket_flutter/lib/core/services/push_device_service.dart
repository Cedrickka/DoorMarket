import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../api/me_api.dart';
import '../models/me.dart';

class PushDeviceService {
  PushDeviceService(this._meApi);

  final MeApi _meApi;

  static const _storedTokenKey = 'dm_push_device_token';

  bool _initialized = false;
  FirebaseMessaging? _messaging;
  void Function(String route)? _onDeepLink;
  StreamSubscription<String>? _tokenRefreshSubscription;
  StreamSubscription<RemoteMessage>? _messageOpenedSubscription;

  Future<void> initialize(
      {required void Function(String route) onDeepLink}) async {
    _onDeepLink = onDeepLink;
    if (_initialized) {
      return;
    }

    try {
      await Firebase.initializeApp();
    } catch (e) {
      final reason = switch (e) {
        FirebaseException fe => '${fe.plugin}:${fe.code}',
        PlatformException pe => '${pe.code}: ${pe.message ?? ''}'.trim(),
        _ => e.runtimeType.toString(),
      };
      debugPrint(
        'PushDeviceService: Firebase initialize skipped (missing config). $reason',
      );
      return;
    }

    _messaging = FirebaseMessaging.instance;
    _initialized = true;

    try {
      await _messaging!.requestPermission(
        alert: true,
        badge: true,
        sound: true,
        provisional: true,
      );
    } catch (e) {
      debugPrint(
          'PushDeviceService: notification permission request failed: $e');
    }

    _tokenRefreshSubscription = _messaging!.onTokenRefresh.listen((token) {
      unawaited(_registerToken(token));
    });

    _messageOpenedSubscription =
        FirebaseMessaging.onMessageOpenedApp.listen((message) {
      _handleOpenFromMessage(message);
    });

    try {
      final initialMessage = await _messaging!.getInitialMessage();
      if (initialMessage != null) {
        _handleOpenFromMessage(initialMessage);
      }
    } catch (e) {
      debugPrint('PushDeviceService: getInitialMessage failed: $e');
    }
  }

  Future<void> syncWithAuth(bool isAuthenticated) async {
    if (!_initialized || _messaging == null) {
      return;
    }

    if (!isAuthenticated) {
      await _unregisterStoredToken();
      return;
    }

    try {
      final token = await _messaging!.getToken();
      if (token == null || token.trim().isEmpty) {
        return;
      }

      await _registerToken(token);
    } catch (e) {
      debugPrint('PushDeviceService: token sync failed: $e');
    }
  }

  Future<void> unregisterForLogout() async {
    if (!_initialized) {
      return;
    }
    await _unregisterStoredToken();
  }

  Future<void> dispose() async {
    await _tokenRefreshSubscription?.cancel();
    await _messageOpenedSubscription?.cancel();
    _tokenRefreshSubscription = null;
    _messageOpenedSubscription = null;
  }

  void _handleOpenFromMessage(RemoteMessage message) {
    final route = _extractDeepLink(message);
    if (route == null) {
      return;
    }

    _onDeepLink?.call(route);
  }

  String? _extractDeepLink(RemoteMessage message) {
    final data = message.data;
    final deepLinkRaw =
        (data['deepLink'] ?? data['deeplink'] ?? data['route'])?.toString();
    final type = (data['type'] ?? '').toString().trim().toLowerCase();

    if (deepLinkRaw != null && deepLinkRaw.trim().isNotEmpty) {
      final normalized = deepLinkRaw.trim();
      return normalized.startsWith('/') ? normalized : '/$normalized';
    }

    if (type == 'cart_recovery') {
      return '/checkout';
    }

    return null;
  }

  Future<void> _registerToken(String token) async {
    final normalized = token.trim();
    if (normalized.isEmpty) {
      return;
    }

    try {
      await _meApi.registerPushDevice(
        RegisterPushDeviceRequest(
          platform: _resolvePlatform(),
          token: normalized,
          deviceId: null,
          deviceModel: null,
          appVersion: null,
        ),
      );

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_storedTokenKey, normalized);
    } catch (e) {
      debugPrint('PushDeviceService: register token failed: $e');
    }
  }

  Future<void> _unregisterStoredToken() async {
    final prefs = await SharedPreferences.getInstance();
    final storedToken = prefs.getString(_storedTokenKey)?.trim() ?? '';
    if (storedToken.isEmpty) {
      await prefs.remove(_storedTokenKey);
      return;
    }

    try {
      await _meApi.unregisterPushDevice(storedToken);
    } catch (e) {
      debugPrint('PushDeviceService: unregister token failed: $e');
    } finally {
      await prefs.remove(_storedTokenKey);
    }
  }

  static String _resolvePlatform() {
    switch (defaultTargetPlatform) {
      case TargetPlatform.iOS:
        return 'apns';
      case TargetPlatform.android:
        return 'fcm';
      default:
        return 'fcm';
    }
  }
}
