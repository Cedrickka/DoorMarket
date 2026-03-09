import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/orders.dart';
import '../../core/providers.dart';

final ordersProvider = FutureProvider<List<OrderDto>>((ref) async {
  final api = ref.read(ordersApiProvider);
  final result = await api.getMine(page: 1, pageSize: 20);
  return result.items;
});

final orderDetailsProvider = FutureProvider.family<OrderDto, String>((ref, id) async {
  final api = ref.read(ordersApiProvider);
  return api.getById(id);
});
