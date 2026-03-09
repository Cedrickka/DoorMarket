import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../models/orders.dart';

class NotificationFeedCache {
  static const _ordersKey = 'dm_notifications_cached_orders';
  static const _syncUtcKey = 'dm_notifications_cached_sync_utc';
  static const _unreadCountKey = 'dm_notifications_cached_unread_count';

  Future<List<OrderDto>> loadCachedOrders() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_ordersKey);
    if (raw == null || raw.isEmpty) {
      return const [];
    }

    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) {
        return const [];
      }

      return decoded
          .whereType<Map>()
          .map(
            (row) => OrderDto.fromJson(
              row.map((key, value) => MapEntry(key.toString(), value)),
            ),
          )
          .toList();
    } catch (_) {
      return const [];
    }
  }

  Future<void> saveCachedOrders(List<OrderDto> orders) async {
    final prefs = await SharedPreferences.getInstance();
    final payload = orders.map((o) => o.toJson()).toList();
    await prefs.setString(_ordersKey, jsonEncode(payload));
  }

  Future<DateTime?> loadCachedSyncUtc() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_syncUtcKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }

    return DateTime.tryParse(raw);
  }

  Future<void> saveCachedSyncUtc(DateTime utc) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_syncUtcKey, utc.toUtc().toIso8601String());
  }

  Future<int> loadCachedUnreadCount() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getInt(_unreadCountKey) ?? 0;
  }

  Future<void> saveCachedUnreadCount(int count) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_unreadCountKey, count < 0 ? 0 : count);
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_ordersKey);
    await prefs.remove(_syncUtcKey);
    await prefs.remove(_unreadCountKey);
  }
}
