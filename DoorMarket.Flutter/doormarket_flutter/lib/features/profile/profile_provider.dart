import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/me.dart';
import '../../core/providers.dart';

final meProvider = FutureProvider<MeDto>((ref) async {
  final api = ref.read(meApiProvider);
  return api.getMe();
});
