import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/marketing_api.dart';
import '../../core/api/me_api.dart';
import '../../core/models/categories.dart';
import '../../core/models/common.dart';
import '../../core/models/marketing.dart';
import '../../core/models/products.dart';
import '../../core/models/shops.dart';
import '../../core/providers.dart';
import '../../core/storage/token_store.dart';

class HomeData {
  final List<CategoryDto> categories;
  final List<MarketingBannerDto> banners;
  final List<ProductDto> promotions;
  final List<ShopDto> shops;
  final List<ProductDto> products;
  final String? displayName;

  HomeData({
    required this.categories,
    required this.banners,
    required this.promotions,
    required this.shops,
    required this.products,
    this.displayName,
  });
}

final homeSearchQueryProvider = StateProvider<String?>((ref) => null);

final homeDataProvider = FutureProvider<HomeData>((ref) async {
  final categoriesApi = ref.read(categoriesApiProvider);
  final marketingApi = ref.read(marketingApiProvider);
  final productsApi = ref.read(productsApiProvider);
  final shopsApi = ref.read(shopsApiProvider);
  final meApi = ref.read(meApiProvider);
  final tokenStore = ref.read(tokenStoreProvider);

  final locale = WidgetsBinding.instance.platformDispatcher.locale;
  final lang = locale.languageCode.toLowerCase();
  final categoriesFuture = categoriesApi.getAll();
  final bannersFuture = _safeLoadBanners(marketingApi, lang);
  final promosFuture =
      productsApi.search(const ProductQuery(promotedOnly: true, pageSize: 8));
  final topProductsFuture =
      productsApi.search(const ProductQuery(pageSize: 16));
  final shopsFuture = shopsApi.search(
      query: const ShopQuery(pageSize: 8), categoryId: null, recommended: null);
  final displayNameFuture = _safeLoadDisplayName(tokenStore, meApi);

  final results = await Future.wait<dynamic>([
    categoriesFuture,
    bannersFuture,
    promosFuture,
    topProductsFuture,
    shopsFuture,
    displayNameFuture,
  ]);

  final categories = results[0] as List<CategoryDto>;
  final banners = results[1] as List<MarketingBannerDto>;
  final promos = results[2] as PagedResult<ProductDto>;
  final topProducts = results[3] as PagedResult<ProductDto>;
  final shops = results[4] as PagedResult<ShopDto>;
  final displayName = results[5] as String?;

  _fireAndForgetTrackImpressions(marketingApi, banners);

  return HomeData(
    categories: categories,
    banners: banners,
    promotions: promos.items,
    shops: shops.items,
    products: topProducts.items,
    displayName: displayName,
  );
});

String _buildDisplayName(String email) {
  if (email.trim().isEmpty) return '';
  final localPart = email.split('@').first.trim();
  if (localPart.isEmpty) return '';
  final cleaned = localPart.replaceAll(RegExp(r'[._-]+'), ' ').trim();
  if (cleaned.isEmpty) return localPart;
  return cleaned
      .split(' ')
      .map((e) => e.isEmpty
          ? ''
          : '${e[0].toUpperCase()}${e.substring(1).toLowerCase()}')
      .join(' ');
}

Future<List<MarketingBannerDto>> _safeLoadBanners(
  MarketingApi marketingApi,
  String language,
) async {
  try {
    return await marketingApi.getBanners(language: language);
  } catch (_) {
    return const <MarketingBannerDto>[];
  }
}

Future<String?> _safeLoadDisplayName(TokenStore tokenStore, MeApi meApi) async {
  try {
    final token = await tokenStore.getAccessToken();
    if (token == null || token.isEmpty) {
      return null;
    }
    final me = await meApi.getMeNoRefresh().timeout(
          const Duration(milliseconds: 900),
        );
    return _buildDisplayName(me.email);
  } catch (_) {
    return null;
  }
}

void _fireAndForgetTrackImpressions(
  MarketingApi marketingApi,
  List<MarketingBannerDto> banners,
) {
  if (banners.isEmpty) {
    return;
  }
  unawaited(() async {
    for (final banner in banners) {
      try {
        await marketingApi.trackImpression(banner.id);
      } catch (_) {
        // Ignore tracking failures; it must not affect home rendering.
      }
    }
  }());
}
