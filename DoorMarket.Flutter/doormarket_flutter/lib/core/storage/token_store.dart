import 'package:shared_preferences/shared_preferences.dart';

class TokenStore {
  static const _accessKey = 'dm_access_token';
  static const _refreshKey = 'dm_refresh_token';
  static const _expiresKey = 'dm_access_expires_utc';

  Future<String?> getAccessToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_accessKey);
  }

  Future<String?> getRefreshToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_refreshKey);
  }

  Future<DateTime?> getExpiresAtUtc() async {
    final prefs = await SharedPreferences.getInstance();
    final value = prefs.getString(_expiresKey);
    if (value == null || value.isEmpty) return null;
    return DateTime.tryParse(value);
  }

  Future<bool> isAccessTokenExpired() async {
    final expires = await getExpiresAtUtc();
    if (expires == null) return true;
    return DateTime.now().toUtc().isAfter(expires);
  }

  Future<void> setTokens({
    required String accessToken,
    required String refreshToken,
    required DateTime expiresAtUtc,
  }) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_accessKey, accessToken);
    await prefs.setString(_refreshKey, refreshToken);
    await prefs.setString(_expiresKey, expiresAtUtc.toUtc().toIso8601String());
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_accessKey);
    await prefs.remove(_refreshKey);
    await prefs.remove(_expiresKey);
  }
}
