import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../features/about/about_screen.dart';
import '../../features/auth/login_screen.dart';
import '../../features/auth/register_screen.dart';
import '../../features/auth/verify_email_screen.dart';
import '../../features/cart/cart_screen.dart';
import '../../features/categories/categories_screen.dart';
import '../../features/categories/category_products_screen.dart';
import '../../features/checkout/checkout_screen.dart';
import '../../features/checkout/payment_return_screen.dart';
import '../../features/home/home_screen.dart';
import '../../features/notifications/notifications_screen.dart';
import '../../features/orders/order_details_screen.dart';
import '../../features/orders/orders_screen.dart';
import '../../features/payments/payments_screen.dart';
import '../../features/product/product_screen.dart';
import '../../features/profile/profile_screen.dart';
import '../../features/promotions/promotions_screen.dart';
import '../../features/returns/returns_screen.dart';
import '../../features/search/search_screen.dart';
import '../../features/settings/settings_screen.dart';
import '../../features/shops/shop_products_screen.dart';
import '../../features/shops/shops_screen.dart';
import '../../features/support/support_screen.dart';
import '../../features/ui_gallery/ui_gallery_screen.dart';
import '../../features/not_found/not_found_screen.dart';
import '../../features/addresses/address_form_screen.dart';
import '../../features/addresses/addresses_screen.dart';
import '../../features/splash/splash_screen.dart';
import '../../features/wishlist/wishlist_screen.dart';
import '../widgets/dm_bottom_nav.dart';

final appRouterProvider = Provider<GoRouter>((ref) {
  final authNotifier = ref.read(authControllerProvider.notifier);
  final themeController = ref.read(themeControllerProvider);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: GoRouterRefreshNotifier(authNotifier.stream),
    routes: [
      GoRoute(path: '/splash', builder: (_, __) => const SplashScreen()),
      GoRoute(path: '/login', builder: (_, __) => const LoginScreen()),
      GoRoute(path: '/register', builder: (_, __) => const RegisterScreen()),
      GoRoute(
        path: '/verify-email',
        builder: (context, state) {
          final email = state.uri.queryParameters['email'] ?? '';
          return VerifyEmailScreen(email: email);
        },
      ),
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) {
          return AppShell(navigationShell: navigationShell);
        },
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: '/', builder: (_, __) => const HomeScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
                path: '/categories',
                builder: (_, __) => const CategoriesScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/cart', builder: (_, __) => const CartScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/orders', builder: (_, __) => const OrdersScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/profile',
              builder: (context, __) => ProfileScreen(
                themeController: themeController,
                onNotifications: () => context.push('/notifications?unread=1'),
                onAbout: () => context.push('/about'),
                onUiGallery: () => context.push('/ui'),
                onAddresses: () => context.push('/addresses'),
                onWishlist: () => context.push('/wishlist'),
                onReturns: () => context.push('/returns'),
                onPayments: () => context.push('/payments'),
                onSupport: () => context.push('/support'),
                onSettings: () => context.push('/settings'),
              ),
            ),
          ]),
        ],
      ),
      GoRoute(
        path: '/notifications',
        builder: (_, state) => NotificationsScreen(
          initialUnreadOnly: state.uri.queryParameters['unread'] == '1',
        ),
      ),
      GoRoute(path: '/about', builder: (_, __) => const AboutScreen()),
      GoRoute(path: '/ui', builder: (_, __) => const UiGalleryScreen()),
      GoRoute(
        path: '/support',
        builder: (_, state) => SupportScreen(
          prefillSubject: state.uri.queryParameters['subject'],
          prefillMessage: state.uri.queryParameters['message'],
          openContactForm: state.uri.queryParameters['openForm'] == '1',
        ),
      ),
      GoRoute(path: '/settings', builder: (_, __) => const SettingsScreen()),
      GoRoute(path: '/addresses', builder: (_, __) => const AddressesScreen()),
      GoRoute(
          path: '/addresses/new',
          builder: (_, __) => const AddressFormScreen()),
      GoRoute(
        path: '/addresses/:id',
        builder: (_, state) =>
            AddressFormScreen(addressId: state.pathParameters['id']),
      ),
      GoRoute(path: '/checkout', builder: (_, __) => const CheckoutScreen()),
      GoRoute(
        path: '/payment-return/:id',
        builder: (_, state) => PaymentReturnScreen(
          orderId: state.pathParameters['id'] ?? '',
          provider: state.uri.queryParameters['provider'] ?? 'PayPal',
          notice: state.uri.queryParameters['notice'],
        ),
      ),
      GoRoute(path: '/payments', builder: (_, __) => const PaymentsScreen()),
      GoRoute(path: '/wishlist', builder: (_, __) => const WishlistScreen()),
      GoRoute(
        path: '/returns',
        builder: (_, state) => ReturnsScreen(
          initialOrderId: state.uri.queryParameters['orderId'],
        ),
      ),
      GoRoute(
        path: '/orders/:id',
        builder: (_, state) =>
            OrderDetailsScreen(orderId: state.pathParameters['id'] ?? ''),
      ),
      GoRoute(path: '/shops', builder: (_, __) => const ShopsScreen()),
      GoRoute(
        path: '/search',
        builder: (_, state) => SearchScreen(
          initialQuery: state.uri.queryParameters['q'],
        ),
      ),
      GoRoute(
        path: '/shops/:id/products',
        builder: (_, state) =>
            ShopProductsScreen(shopId: state.pathParameters['id'] ?? ''),
      ),
      GoRoute(
          path: '/promotions', builder: (_, __) => const PromotionsScreen()),
      GoRoute(
        path: '/categories/:id',
        builder: (_, state) => CategoryProductsScreen(
            categoryId: state.pathParameters['id'] ?? ''),
      ),
      GoRoute(
        path: '/product/:id',
        builder: (_, state) =>
            ProductScreen(productId: state.pathParameters['id'] ?? ''),
      ),
    ],
    errorBuilder: (_, __) => const NotFoundScreen(),
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final location = state.uri.path;
      final isAuthPage = location == '/login' ||
          location == '/register' ||
          location == '/verify-email' ||
          location == '/splash';
      final isProtected =
          _protectedPaths.any((path) => location.startsWith(path));

      if (auth.isLoading) {
        return location == '/splash' ? null : '/splash';
      }

      if (location == '/splash') {
        return auth.isAuthenticated ? '/' : '/login';
      }

      if (!auth.isAuthenticated && isProtected) {
        return '/login';
      }

      if (auth.isAuthenticated && isAuthPage) {
        return '/';
      }

      return null;
    },
  );
});

const _protectedPaths = [
  '/orders',
  '/profile',
  '/checkout',
  '/payments',
  '/returns',
  '/addresses',
];

class AppShell extends StatelessWidget {
  final StatefulNavigationShell navigationShell;

  const AppShell({
    super.key,
    required this.navigationShell,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: DmBottomNav(
        currentIndex: navigationShell.currentIndex,
        onTap: (index) => navigationShell.goBranch(index),
      ),
    );
  }
}

class GoRouterRefreshNotifier extends ChangeNotifier {
  GoRouterRefreshNotifier(Stream<dynamic> stream) {
    _subscription = stream.listen((_) => notifyListeners());
  }

  late final StreamSubscription<dynamic> _subscription;

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
