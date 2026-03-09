import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/categories.dart';
import '../../core/models/products.dart';
import '../../core/providers.dart';

class CategoriesData {
  final List<CategoryDto> categories;
  final List<ProductDto> products;

  CategoriesData({required this.categories, required this.products});
}

final categoriesSearchQueryProvider = StateProvider<String>((ref) => '');
final categoriesSelectedCategoryIdProvider =
    StateProvider<String?>((ref) => null);

final categoriesDataProvider = FutureProvider<CategoriesData>((ref) async {
  final categoriesApi = ref.read(categoriesApiProvider);
  final productsApi = ref.read(productsApiProvider);

  final categories = await categoriesApi.getAll();
  final products = await productsApi.search(
    const ProductQuery(
      pageSize: 60,
    ),
  );

  return CategoriesData(categories: categories, products: products.items);
});
