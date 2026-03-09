import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/me.dart';
import '../../core/providers.dart';

final addressesProvider = FutureProvider<List<AddressDto>>((ref) async {
  final api = ref.read(addressApiProvider);
  return api.getAddresses();
});
