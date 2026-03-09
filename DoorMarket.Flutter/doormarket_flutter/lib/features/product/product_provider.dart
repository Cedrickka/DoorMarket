import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/products.dart';
import '../../core/providers.dart';

final productProvider = FutureProvider.family<ProductDto, String>((ref, id) async {
  final api = ref.read(productsApiProvider);
  return api.getById(id);
});
