import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../models/saved_product.dart';

class WishlistStore {
  static const _key = 'dm_mobile_wishlist_v1';

  Future<List<SavedProductEntry>> getItems() async {
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
      return items;
    } catch (_) {
      return const [];
    }
  }

  Future<bool> contains(String productId) async {
    final items = await getItems();
    return items.any((x) => x.productId == productId);
  }

  Future<bool> toggle(SavedProductEntry entry) async {
    final items = (await getItems()).toList();
    final index = items.indexWhere((x) => x.productId == entry.productId);
    if (index >= 0) {
      items.removeAt(index);
      await _save(items);
      return false;
    }

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
    await _save(items);
    return true;
  }

  Future<void> remove(String productId) async {
    final items = (await getItems()).where((x) => x.productId != productId).toList();
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
