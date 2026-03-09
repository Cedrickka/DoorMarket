import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/address_api.dart';
import 'api/admin_notifications_api.dart';
import 'api/auth_api.dart';
import 'api/cart_api.dart';
import 'api/categories_api.dart';
import 'api/checkout_analytics_api.dart';
import 'api/delivery_api.dart';
import 'api/loyalty_api.dart';
import 'api/me_api.dart';
import 'api/orders_api.dart';
import 'api/payments_api.dart';
import 'api/products_api.dart';
import 'api/returns_api.dart';
import 'api/search_api.dart';
import 'api/shops_api.dart';
import 'api/support_api.dart';
import 'api/marketing_api.dart';
import 'auth/auth_controller.dart';
import 'auth/auth_session.dart';
import 'auth/auth_state.dart';
import 'config/app_config.dart';
import 'models/saved_product.dart';
import 'notifications/notification_preferences_controller.dart';
import 'network/api_client.dart';
import 'storage/notification_feed_cache.dart';
import 'storage/recently_viewed_store.dart';
import 'storage/token_store.dart';
import 'storage/wishlist_store.dart';
import 'services/push_device_service.dart';
import 'services/checkout_observability_service.dart';
import 'theme/locale_controller.dart';
import 'theme/theme_controller.dart';
import 'i18n/app_strings.dart';
import 'services/tts_service.dart';

final themeControllerProvider =
    ChangeNotifierProvider((ref) => ThemeController());
final localeControllerProvider =
    StateNotifierProvider<LocaleController, Locale?>(
        (ref) => LocaleController());
final notificationPreferencesProvider =
    ChangeNotifierProvider((ref) => NotificationPreferencesController());
final stringsProvider = Provider<AppStrings>((ref) {
  final locale = ref.watch(localeControllerProvider);
  final resolved = locale ?? WidgetsBinding.instance.platformDispatcher.locale;
  return AppStrings(resolved);
});

final tokenStoreProvider = Provider((ref) => TokenStore());
final notificationFeedCacheProvider =
    Provider((ref) => NotificationFeedCache());
final wishlistStoreProvider = Provider((ref) => WishlistStore());
final recentlyViewedStoreProvider = Provider((ref) => RecentlyViewedStore());
final wishlistItemsProvider = FutureProvider<List<SavedProductEntry>>((ref) {
  return ref.read(wishlistStoreProvider).getItems();
});
final recentlyViewedItemsProvider =
    FutureProvider<List<SavedProductEntry>>((ref) {
  return ref.read(recentlyViewedStoreProvider).getItems(take: 8);
});

final rawDioProvider = Provider((ref) {
  return Dio(BaseOptions(
    baseUrl: AppConfig.apiBaseUrl,
    connectTimeout: const Duration(seconds: 20),
    receiveTimeout: const Duration(seconds: 20),
    sendTimeout: const Duration(seconds: 20),
  ));
});

final authClientProvider = Provider((ref) {
  final tokenStore = ref.read(tokenStoreProvider);
  return ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    tokenStore: tokenStore,
    onRefresh: () async => false,
    onLogout: () async {},
  );
});

final authApiProvider =
    Provider((ref) => AuthApi(ref.read(authClientProvider)));

final authSessionProvider = Provider((ref) {
  return AuthSession(
    authApi: ref.read(authApiProvider),
    tokenStore: ref.read(tokenStoreProvider),
    rawHttp: ref.read(rawDioProvider),
  );
});

final apiClientProvider = Provider((ref) {
  final tokenStore = ref.read(tokenStoreProvider);
  final session = ref.read(authSessionProvider);
  return ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    tokenStore: tokenStore,
    onRefresh: session.tryRefresh,
    onLogout: session.signOut,
  );
});

final meApiProvider = Provider((ref) => MeApi(ref.read(apiClientProvider)));
final pushDeviceServiceProvider =
    Provider((ref) => PushDeviceService(ref.read(meApiProvider)));
final categoriesApiProvider =
    Provider((ref) => CategoriesApi(ref.read(apiClientProvider)));
final productsApiProvider =
    Provider((ref) => ProductsApi(ref.read(apiClientProvider)));
final cartApiProvider = Provider((ref) => CartApi(ref.read(apiClientProvider)));
final checkoutAnalyticsApiProvider =
    Provider((ref) => CheckoutAnalyticsApi(ref.read(apiClientProvider)));
final checkoutObservabilityServiceProvider = Provider(
    (ref) => CheckoutObservabilityService(ref.read(checkoutAnalyticsApiProvider)));
final ordersApiProvider =
    Provider((ref) => OrdersApi(ref.read(apiClientProvider)));
final returnsApiProvider =
    Provider((ref) => ReturnsApi(ref.read(apiClientProvider)));
final deliveryApiProvider =
    Provider((ref) => DeliveryApi(ref.read(apiClientProvider)));
final paymentsApiProvider =
    Provider((ref) => PaymentsApi(ref.read(apiClientProvider)));
final shopsApiProvider =
    Provider((ref) => ShopsApi(ref.read(apiClientProvider)));
final searchApiProvider =
    Provider((ref) => SearchApi(ref.read(apiClientProvider)));
final addressApiProvider =
    Provider((ref) => AddressApi(ref.read(apiClientProvider)));
final supportApiProvider =
    Provider((ref) => SupportApi(ref.read(apiClientProvider)));
final marketingApiProvider =
    Provider((ref) => MarketingApi(ref.read(apiClientProvider)));
final loyaltyApiProvider =
    Provider((ref) => LoyaltyApi(ref.read(apiClientProvider)));
final adminNotificationsApiProvider =
    Provider((ref) => AdminNotificationsApi(ref.read(apiClientProvider)));
final ttsProvider = Provider((ref) => TtsService());

final authControllerProvider =
    StateNotifierProvider<AuthController, AuthState>((ref) {
  return AuthController(
    session: ref.read(authSessionProvider),
    authApi: ref.read(authApiProvider),
    meApi: ref.read(meApiProvider),
    pushDeviceService: ref.read(pushDeviceServiceProvider),
  );
});
