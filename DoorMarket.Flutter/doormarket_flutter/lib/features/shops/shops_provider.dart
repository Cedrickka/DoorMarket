import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/categories.dart';
import '../../core/models/shops.dart';
import '../../core/providers.dart';

class ShopsData {
  final List<ShopDto> shops;
  final List<CategoryDto> categories;

  ShopsData({required this.shops, required this.categories});
}

final shopsSearchProvider = StateProvider<String?>((ref) => null);
final shopsCountryProvider = StateProvider<String?>((ref) => null);
final shopsRecommendedProvider = StateProvider<bool>((ref) => false);
final shopsCategoryProvider = StateProvider<String?>((ref) => null);

final shopsDataProvider = FutureProvider<ShopsData>((ref) async {
  final shopsApi = ref.read(shopsApiProvider);
  final categoriesApi = ref.read(categoriesApiProvider);

  final search = ref.watch(shopsSearchProvider);
  final country = ref.watch(shopsCountryProvider);
  final recommended = ref.watch(shopsRecommendedProvider);
  final categoryId = ref.watch(shopsCategoryProvider);

  final categories = await categoriesApi.getAll();
  final result = await shopsApi.search(
    query: ShopQuery(pageSize: 30, q: search, countryTag: country),
    categoryId: categoryId,
    recommended: recommended ? true : null,
  );

  return ShopsData(shops: result.items, categories: categories);
});
