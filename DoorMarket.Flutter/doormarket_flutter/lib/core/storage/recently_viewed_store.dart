import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../models/saved_product.dart';

class RecentlyViewedStore {
  static const _key = 'dm_mobile_recently_viewed_v1';
  static const _maxItems = 40;

  Future<List<SavedProductEntry>> getItems({int take = 12}) async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_key);
    if (raw == null || raw.isEmpty) {
      return const [];
    }

    try {
      final decoded = jsonDecode(raw);
      if (decoded is! List) {
        return const [];
      }

      final items = decoded
          .whereType<Map>()
          .map((row) => SavedProductEntry.fromJson(
                row.map((key, value) => MapEntry(key.toString(), value)),
              ))
          .toList();

      items.sort((a, b) => b.savedAtUtc.compareTo(a.savedAtUtc));
      final limit = take <= 0 ? 12 : take;
      return items.take(limit).toList();
    } catch (_) {
      return const [];
    }
  }

  Future<void> track(SavedProductEntry entry) async {
    final items = (await getItems(take: _maxItems)).toList();
    items.removeWhere((x) => x.productId == entry.productId);
    items.insert(
      0,
      SavedProductEntry(
        productId: entry.productId,
        shopId: entry.shopId,
        shopName: entry.shopName,
        productName: entry.productName,
        imageUrl: entry.imageUrl,
        price: entry.price,
        effectivePrice: entry.effectivePrice,
        hasActivePromotion: entry.hasActivePromotion,
        currency: entry.currency,
        savedAtUtc: DateTime.now().toUtc(),
      ),
    );

    if (items.length > _maxItems) {
      items.removeRange(_maxItems, items.length);
    }

    await _save(items);
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_key);
  }

  Future<void> _save(List<SavedProductEntry> items) async {
    final prefs = await SharedPreferences.getInstance();
    final payload = items.map((x) => x.toJson()).toList();
    await prefs.setString(_key, jsonEncode(payload));
  }
}
