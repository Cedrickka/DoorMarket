import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/models/categories.dart';
import '../../core/models/marketing.dart';
import '../../core/models/products.dart';
import '../../core/models/saved_product.dart';
import '../../core/models/shops.dart';
import '../../core/i18n/app_strings.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_badge_promo.dart';
import '../../core/widgets/dm_brand_mark.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_fade_slide.dart';
import '../../core/widgets/dm_language_badge.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';
import '../../core/widgets/dm_secondary_button.dart';
import '../../core/providers.dart';
import '../../core/services/user_error_service.dart';
import '../cart/cart_provider.dart';
import '../notifications/notification_counters_provider.dart';
import 'home_provider.dart';

class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key});

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen>
    with AutomaticKeepAliveClientMixin {
  final _searchController = TextEditingController();
  final _bannerController = PageController(viewportFraction: 0.92);
  final _promoController = PageController(viewportFraction: 0.92);
  final ValueNotifier<int> _bannerIndex = ValueNotifier<int>(0);
  final ValueNotifier<int> _promoIndex = ValueNotifier<int>(0);
  Timer? _bannerAutoScroll;
  Timer? _promoAutoScroll;
  Timer? _homeSearchDebounce;
  final Set<String> _addingToCartIds = <String>{};
  int _bannerItemsCount = 0;
  int _promoItemsCount = 0;
  String _homeQuery = '';

  @override
  void initState() {
    super.initState();
    _bannerAutoScroll = Timer.periodic(const Duration(seconds: 6), (_) {
      if (!mounted || !_bannerController.hasClients || _bannerItemsCount <= 1) {
        return;
      }
      final currentPage = _bannerController.page?.round() ?? 0;
      final nextPage = (currentPage + 1) % _bannerItemsCount;
      _safeAnimateToPage(_bannerController, nextPage);
    });
    _promoAutoScroll = Timer.periodic(const Duration(seconds: 5), (_) {
      if (!mounted || !_promoController.hasClients || _promoItemsCount <= 1) {
        return;
      }
      final currentPage = _promoController.page?.round() ?? 0;
      final nextPage = (currentPage + 1) % _promoItemsCount;
      _safeAnimateToPage(_promoController, nextPage);
    });
  }

  @override
  void dispose() {
    _homeSearchDebounce?.cancel();
    _bannerAutoScroll?.cancel();
    _promoAutoScroll?.cancel();
    _searchController.dispose();
    _bannerIndex.dispose();
    _promoIndex.dispose();
    _bannerController.dispose();
    _promoController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final data = ref.watch(homeDataProvider);
    final strings = ref.watch(stringsProvider);
    final unreadNotifications = ref.watch(unreadNotificationsCountProvider);
    final wishlistAsync = ref.watch(wishlistItemsProvider);
    final recentlyViewedAsync = ref.watch(recentlyViewedItemsProvider);
    final unreadNotificationsCount =
        unreadNotifications.maybeWhen(data: (value) => value, orElse: () => 0);
    final wishlistCount = wishlistAsync.maybeWhen(
      data: (items) => items.length,
      orElse: () => 0,
    );
    final locale = Localizations.localeOf(context);
    final isEn = locale.languageCode.toLowerCase() == 'en';
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: data.when(
        loading: () => _homeSkeleton(context),
        error: (err, _) => _homeErrorState(context, strings, err),
        data: (value) {
          final normalizedQuery = _normalizeQuery(_homeQuery);
          final filteredProducts =
              _filterProducts(value.products, normalizedQuery);
          final filteredPromotions =
              _filterProducts(value.promotions, normalizedQuery);

          return RefreshIndicator(
            onRefresh: _refreshHome,
            child: CustomScrollView(
              key: const PageStorageKey<String>('home-scroll'),
              cacheExtent: 900,
              physics: const AlwaysScrollableScrollPhysics(
                parent: BouncingScrollPhysics(),
              ),
              slivers: [
                SliverToBoxAdapter(
                  child: _heroHeader(
                    context,
                    value,
                    strings,
                    unreadNotificationsCount: unreadNotificationsCount,
                    wishlistCount: wishlistCount,
                  ),
                ),
                SliverAppBar(
                  pinned: true,
                  automaticallyImplyLeading: false,
                  backgroundColor:
                      isDark ? DmColors.surfaceDark : DmColors.surfaceLight,
                  surfaceTintColor: Colors.transparent,
                  elevation: 0,
                  scrolledUnderElevation: 0,
                  toolbarHeight: 82,
                  titleSpacing: 0,
                  title: _stickySearchBar(context, strings),
                ),
                SliverPadding(
                  padding: EdgeInsets.zero,
                  sliver: SliverToBoxAdapter(
                    child: DmFadeSlide(
                      delay: const Duration(milliseconds: 80),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const SizedBox(height: 10),
                          if (value.banners.isNotEmpty)
                            _bannerCarousel(context, value.banners),
                          if (value.banners.isNotEmpty)
                            const SizedBox(height: 20),
                          if (value.banners.isEmpty &&
                              value.promotions.isNotEmpty)
                            _promoBanner(context),
                          if (value.banners.isEmpty &&
                              value.promotions.isNotEmpty)
                            const SizedBox(height: 20),
                          _sectionHeader(
                              strings.tr('Categories', 'Categories'), context),
                          const SizedBox(height: 12),
                          SizedBox(
                            height: 44,
                            child: ListView.separated(
                              scrollDirection: Axis.horizontal,
                              itemBuilder: (context, index) => _categoryTab(
                                context,
                                value.categories[index],
                                isEn: isEn,
                              ),
                              separatorBuilder: (_, __) =>
                                  const SizedBox(width: 6),
                              itemCount: value.categories.length,
                            ),
                          ),
                          const SizedBox(height: 22),
                          _sectionHeader(
                            strings.tr('Offres speciales', 'Special offers'),
                            context,
                            action: filteredPromotions.isEmpty
                                ? null
                                : strings.tr('Voir tout', 'View all'),
                            onAction: filteredPromotions.isEmpty
                                ? null
                                : () => context.push('/promotions'),
                          ),
                          const SizedBox(height: 12),
                          _promotionsSection(context, filteredPromotions),
                          const SizedBox(height: 22),
                          _sectionHeader(
                            strings.tr(
                                'Recemment consultes', 'Recently viewed'),
                            context,
                          ),
                          const SizedBox(height: 12),
                          _recentlyViewedSection(context, recentlyViewedAsync),
                          const SizedBox(height: 22),
                          _sectionHeader(
                            strings.tr('Wishlist', 'Wishlist'),
                            context,
                            action: strings.tr('Voir tout', 'View all'),
                            onAction: () => context.push('/wishlist'),
                          ),
                          const SizedBox(height: 12),
                          _wishlistSection(context, wishlistAsync),
                          const SizedBox(height: 22),
                          _sectionHeader(
                            strings.tr('Boutiques populaires', 'Popular shops'),
                            context,
                            action: strings.tr('Voir tout', 'View all'),
                            onAction: () => context.push('/shops'),
                          ),
                          const SizedBox(height: 12),
                          SizedBox(
                            height: 180,
                            child: ListView.separated(
                              scrollDirection: Axis.horizontal,
                              itemBuilder: (context, index) =>
                                  _shopCard(context, value.shops[index]),
                              separatorBuilder: (_, __) =>
                                  const SizedBox(width: 12),
                              itemCount: value.shops.length,
                            ),
                          ),
                          const SizedBox(height: 22),
                          _sectionHeader(
                            strings.tr(
                                'Produits vedettes', 'Featured products'),
                            context,
                            action: strings.tr('Voir tout', 'View all'),
                            onAction: () => context.push('/categories'),
                          ),
                          const SizedBox(height: 12),
                        ],
                      ),
                    ),
                  ),
                ),
                if (filteredProducts.isEmpty)
                  SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(0, 8, 0, 20),
                      child: Center(
                        child: Text(
                          strings.tr(
                            'Aucun produit pour cette recherche.',
                            'No products for this search.',
                          ),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ),
                    ),
                  )
                else
                  SliverPadding(
                    padding: EdgeInsets.zero,
                    sliver: SliverGrid(
                      gridDelegate:
                          const SliverGridDelegateWithFixedCrossAxisCount(
                        crossAxisCount: 2,
                        crossAxisSpacing: 4,
                        mainAxisSpacing: 4,
                        childAspectRatio: 0.76,
                      ),
                      delegate: SliverChildBuilderDelegate(
                        (context, index) =>
                            _productCard(context, filteredProducts[index]),
                        childCount: filteredProducts.length,
                      ),
                    ),
                  ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(0, 16, 0, 32),
                    child: DmFadeSlide(
                      delay: const Duration(milliseconds: 140),
                      child: DmCard(
                        margin: EdgeInsets.zero,
                        child: Row(
                          children: [
                            Container(
                              height: 46,
                              width: 46,
                              decoration: BoxDecoration(
                                color: DmColors.iconBg(isDark),
                                borderRadius: DmRadius.r14,
                              ),
                              child: const Icon(Icons.local_shipping,
                                  color: DmColors.doorOrange),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                strings.tr(
                                  'Livraison rapide, suivi en temps reel et paiements securises.',
                                  'Fast delivery, real-time tracking, and secure payments.',
                                ),
                                style: Theme.of(context).textTheme.bodyMedium,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  @override
  bool get wantKeepAlive => true;

  Widget _homeErrorState(
    BuildContext context,
    AppStrings strings,
    Object error,
  ) {
    final message = UserErrorService.message(
      context,
      error,
      fallbackFr: 'Le chargement de la page d accueil est indisponible.',
      fallbackEn: 'Home page loading is currently unavailable.',
    );
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: DmCard(
          margin: EdgeInsets.zero,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.error_outline,
                color: isDark ? DmColors.errorDark : DmColors.errorLight,
                size: 24,
              ),
              const SizedBox(height: 8),
              Text(
                message,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: 12),
              DmSecondaryButton(
                height: 42,
                label: strings.tr('Reessayer', 'Retry'),
                onPressed: () => unawaited(_refreshHome()),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _heroHeader(
    BuildContext context,
    HomeData value,
    AppStrings strings, {
    required int unreadNotificationsCount,
    required int wishlistCount,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final displayName = value.displayName?.trim() ?? '';
    final firstName = displayName.isEmpty ? '' : displayName.split(' ').first;
    final name = firstName.isEmpty
        ? strings.tr('Bonjour', 'Hello')
        : strings.tr('Bonjour, $firstName', 'Hello, $firstName');

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            DmColors.doorBlue,
            DmColors.doorBlue.withAlpha(isDark ? 235 : 245),
          ],
        ),
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(28),
          bottomRight: Radius.circular(28),
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 14, 24, 14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const DmBrandMark(size: 30, monochromeWhite: true),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'DoorMarket',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      strings.splashTagline,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          color: Color(0xFFD9E5F8), fontSize: 12),
                    ),
                  ),
                  const DmLanguageBadge(),
                  const SizedBox(width: 8),
                  _iconButton(Icons.notifications_none, context,
                      badgeCount: unreadNotificationsCount,
                      onTap: () => context.push(
                            unreadNotificationsCount > 0
                                ? '/notifications?unread=1'
                                : '/notifications',
                          )),
                  const SizedBox(width: 8),
                  _iconButton(Icons.favorite_border, context,
                      badgeCount: wishlistCount,
                      onTap: () => context.push('/wishlist')),
                  const SizedBox(width: 8),
                  _iconButton(Icons.shopping_bag_outlined, context,
                      onTap: () => context.push('/cart')),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                strings.tr('Decouvrez les meilleurs produits frais du marche.',
                    'Discover the best fresh market products.'),
                style: const TextStyle(color: Color(0xFFD9E5F8), fontSize: 13),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _refreshHome() async {
    ref.invalidate(homeDataProvider);
    ref.invalidate(unreadNotificationsCountProvider);
    ref.invalidate(wishlistItemsProvider);
    ref.invalidate(recentlyViewedItemsProvider);
    try {
      await ref.read(homeDataProvider.future);
    } catch (_) {}
  }

  Widget _stickySearchBar(BuildContext context, AppStrings strings) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? DmColors.surfaceDark : DmColors.surfaceLight;
    return Container(
      color: bg,
      padding: const EdgeInsets.fromLTRB(0, 12, 0, 8),
      child: DmSearchBar(
        hint: strings.tr('Produits, boutiques, categories...',
            'Products, shops, categories...'),
        controller: _searchController,
        onChanged: _onHomeSearchChanged,
        onSubmitted: (value) => _applyHomeSearchQuery(value),
        trailingIcon: _homeQuery.trim().isEmpty ? Icons.search : Icons.clear,
        onTrailingTap: _homeQuery.trim().isEmpty
            ? null
            : () {
                _searchController.clear();
                _applyHomeSearchQuery('');
              },
      ),
    );
  }

  void _onHomeSearchChanged(String value) {
    _homeSearchDebounce?.cancel();
    _homeSearchDebounce = Timer(const Duration(milliseconds: 220), () {
      _applyHomeSearchQuery(value);
    });
  }

  void _applyHomeSearchQuery(String value) {
    final query = value.trim();
    setState(() => _homeQuery = query);
    ref.read(homeSearchQueryProvider.notifier).state =
        query.isEmpty ? null : query;
  }

  Widget _promotionsSection(BuildContext context, List<ProductDto> promotions) {
    final strings = ref.read(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    _promoItemsCount = promotions.length;
    if (promotions.isEmpty) {
      return DmCard(
        margin: EdgeInsets.zero,
        child: Row(
          children: [
            Container(
              height: 42,
              width: 42,
              decoration: BoxDecoration(
                color: DmColors.iconBg(isDark),
                borderRadius: DmRadius.r12,
              ),
              child: const Icon(Icons.local_offer_outlined,
                  color: DmColors.doorOrange),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                strings.tr(
                  'Aucune promotion active pour le moment.',
                  'No active promotions at the moment.',
                ),
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          ],
        ),
      );
    }

    return Column(
      children: [
        SizedBox(
          height: 236,
          child: PageView.builder(
            controller: _promoController,
            itemCount: promotions.length,
            onPageChanged: (index) => _promoIndex.value = index,
            itemBuilder: (context, index) {
              final product = promotions[index];
              return _promoCard(context, product);
            },
          ),
        ),
        const SizedBox(height: 8),
        ValueListenableBuilder<int>(
          valueListenable: _promoIndex,
          builder: (context, promoIndex, _) => Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(
              promotions.length,
              (index) => AnimatedContainer(
                duration: const Duration(milliseconds: 220),
                width: promoIndex == index ? 18 : 6,
                height: 6,
                margin: const EdgeInsets.symmetric(horizontal: 3),
                decoration: BoxDecoration(
                  color: promoIndex == index
                      ? DmColors.doorOrange
                      : DmColors.border(isDark),
                  borderRadius: BorderRadius.circular(20),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _promoBanner(BuildContext context) {
    final strings = ref.read(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final gradient = LinearGradient(
      colors: [
        DmColors.doorOrange,
        DmColors.doorOrange.withAlpha(isDark ? 210 : 240),
      ],
      begin: Alignment.topLeft,
      end: Alignment.bottomRight,
    );

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: gradient,
        borderRadius: DmRadius.r20,
        boxShadow: const [
          BoxShadow(
              color: Color(0x33000000), blurRadius: 18, offset: Offset(0, 10)),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  strings.tr('-20% sur les fruits', '-20% on fruits'),
                  style: Theme.of(context)
                      .textTheme
                      .headlineSmall
                      ?.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 6),
                Text(
                  strings.tr(
                    'Profitez des produits frais du marche et d\'offres limitees.',
                    'Enjoy fresh market products and limited offers.',
                  ),
                  style:
                      const TextStyle(color: Color(0xFFFFF4E6), fontSize: 12),
                ),
                const SizedBox(height: 12),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: Colors.white.withAlpha(46),
                    borderRadius: DmRadius.r16,
                  ),
                  child: Text(
                    strings.tr('Limite', 'Limited'),
                    style: const TextStyle(color: Colors.white, fontSize: 11),
                  ),
                ),
              ],
            ),
          ),
          Container(
            height: 46,
            width: 46,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: DmRadius.r16,
            ),
            child: const Icon(Icons.arrow_forward, color: DmColors.doorOrange),
          ),
        ],
      ),
    );
  }

  Widget _recentlyViewedSection(
    BuildContext context,
    AsyncValue<List<SavedProductEntry>> data,
  ) {
    final strings = ref.read(stringsProvider);
    return data.when(
      loading: () => const SizedBox(
        height: 108,
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (_, __) => SizedBox(
        height: 88,
        child: Align(
          alignment: Alignment.centerLeft,
          child: Text(
            strings.tr('Impossible de charger', 'Unable to load'),
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ),
      ),
      data: (items) {
        if (items.isEmpty) {
          return SizedBox(
            height: 88,
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                strings.tr(
                  'Aucun produit recemment consulte',
                  'No recently viewed products',
                ),
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          );
        }

        return SizedBox(
          height: 214,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(width: 6),
            itemBuilder: (context, index) =>
                _savedMiniCard(context, items[index]),
          ),
        );
      },
    );
  }

  Widget _wishlistSection(
    BuildContext context,
    AsyncValue<List<SavedProductEntry>> data,
  ) {
    final strings = ref.read(stringsProvider);
    return data.when(
      loading: () => const SizedBox(
        height: 108,
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (_, __) => SizedBox(
        height: 88,
        child: Align(
          alignment: Alignment.centerLeft,
          child: Text(
            strings.tr('Impossible de charger', 'Unable to load'),
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ),
      ),
      data: (items) {
        if (items.isEmpty) {
          return SizedBox(
            height: 88,
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                strings.tr('Votre wishlist est vide', 'Your wishlist is empty'),
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          );
        }

        return SizedBox(
          height: 214,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: items.length > 8 ? 8 : items.length,
            separatorBuilder: (_, __) => const SizedBox(width: 6),
            itemBuilder: (context, index) => _savedMiniCard(
              context,
              items[index],
              showRemove: true,
            ),
          ),
        );
      },
    );
  }

  Widget _savedMiniCard(
    BuildContext context,
    SavedProductEntry item, {
    bool showRemove = false,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return RepaintBoundary(
      child: SizedBox(
        width: 182,
        child: InkWell(
          onTap: () => context.push('/product/${item.productId}'),
          borderRadius: DmRadius.r16,
          child: DmCard(
            margin: EdgeInsets.zero,
            padding: const EdgeInsets.all(8),
            boxShadowOverride: const [],
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Stack(
                    children: [
                      Positioned.fill(
                        child: DmNetworkImage(
                          url: item.imageUrl,
                          borderRadius: DmRadius.r12,
                          fit: BoxFit.cover,
                          cacheWidth: 320,
                          cacheHeight: 260,
                        ),
                      ),
                      if (showRemove)
                        Positioned(
                          top: 6,
                          right: 6,
                          child: InkWell(
                            borderRadius: BorderRadius.circular(16),
                            onTap: () =>
                                _removeWishlistFromHome(item.productId),
                            child: Container(
                              height: 28,
                              width: 28,
                              decoration: BoxDecoration(
                                color: Colors.black.withAlpha(100),
                                borderRadius: BorderRadius.circular(16),
                              ),
                              child: const Icon(
                                Icons.favorite,
                                color: Colors.white,
                                size: 16,
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  item.productName,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                ),
                const SizedBox(height: 2),
                Text(
                  item.shopName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        fontSize: 11,
                        color: DmColors.mutedText(isDark),
                      ),
                ),
                const SizedBox(height: 4),
                Text(
                  '${item.currency} ${item.effectivePrice.toStringAsFixed(2)}',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        fontSize: 12,
                        color: DmColors.doorOrange,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _removeWishlistFromHome(String productId) async {
    final strings = ref.read(stringsProvider);
    await ref.read(wishlistStoreProvider).remove(productId);
    ref.invalidate(wishlistItemsProvider);
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          strings.tr(
            'Produit retire de la wishlist',
            'Product removed from wishlist',
          ),
        ),
      ),
    );
  }

  Widget _sectionHeader(String title, BuildContext context,
      {String? action, VoidCallback? onAction}) {
    final actionStyle = Theme.of(context)
        .textTheme
        .bodySmall
        ?.copyWith(color: DmColors.doorOrange, fontWeight: FontWeight.w600);
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: Theme.of(context).textTheme.headlineMedium),
        if (action != null)
          InkWell(
            onTap: onAction,
            child: Text(action, style: actionStyle),
          ),
      ],
    );
  }

  Widget _bannerCarousel(
      BuildContext context, List<MarketingBannerDto> banners) {
    final strings = ref.read(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final cardColor = isDark ? DmColors.surfaceDark : DmColors.surfaceLight;
    _bannerItemsCount = banners.length;

    return Column(
      children: [
        SizedBox(
          height: 168,
          child: PageView.builder(
            controller: _bannerController,
            itemCount: banners.length,
            onPageChanged: (index) => _bannerIndex.value = index,
            itemBuilder: (context, index) {
              final banner = banners[index];
              return InkWell(
                onTap: () => _handleBannerTap(context, banner),
                borderRadius: DmRadius.r20,
                child: DmCard(
                  padding: EdgeInsets.zero,
                  margin: EdgeInsets.zero,
                  boxShadowOverride: const [],
                  child: Container(
                    decoration: BoxDecoration(
                      color: cardColor,
                      borderRadius: DmRadius.r20,
                    ),
                    child: Stack(
                      children: [
                        Positioned.fill(
                          child: DmNetworkImage(
                            url: banner.imageUrl,
                            fit: BoxFit.cover,
                            borderRadius: DmRadius.r20,
                            cacheWidth: 720,
                            cacheHeight: 360,
                          ),
                        ),
                        Positioned.fill(
                          child: Container(
                            decoration: BoxDecoration(
                              borderRadius: DmRadius.r20,
                              gradient: LinearGradient(
                                colors: [
                                  Colors.black.withAlpha(140),
                                  Colors.black.withAlpha(20),
                                ],
                                begin: Alignment.bottomLeft,
                                end: Alignment.topRight,
                              ),
                            ),
                          ),
                        ),
                        Positioned(
                          left: 16,
                          bottom: 16,
                          right: 16,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                banner.title,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 18,
                                  fontWeight: FontWeight.w700,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                              if (banner.subtitle != null &&
                                  banner.subtitle!.isNotEmpty) ...[
                                const SizedBox(height: 4),
                                Text(
                                  banner.subtitle!,
                                  style: const TextStyle(
                                      color: Color(0xFFE6EEF9), fontSize: 12),
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ],
                              const SizedBox(height: 8),
                              Row(
                                children: [
                                  Text(
                                    strings.tr('Decouvrir', 'Discover'),
                                    style: const TextStyle(
                                        color: Colors.white,
                                        fontWeight: FontWeight.w600),
                                  ),
                                  const SizedBox(width: 6),
                                  const Icon(Icons.arrow_forward,
                                      color: Colors.white, size: 16),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ),
        const SizedBox(height: 8),
        ValueListenableBuilder<int>(
          valueListenable: _bannerIndex,
          builder: (context, bannerIndex, _) => Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(
              banners.length,
              (index) => AnimatedContainer(
                duration: const Duration(milliseconds: 200),
                height: 6,
                width: bannerIndex == index ? 18 : 6,
                margin: const EdgeInsets.symmetric(horizontal: 3),
                decoration: BoxDecoration(
                  color: bannerIndex == index
                      ? DmColors.doorOrange
                      : DmColors.border(isDark),
                  borderRadius: BorderRadius.circular(20),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Future<void> _handleBannerTap(
      BuildContext context, MarketingBannerDto banner) async {
    if (banner.id.isNotEmpty) {
      try {
        await ref.read(marketingApiProvider).trackClick(banner.id);
      } catch (_) {}
    }

    final target = banner.targetUrl;
    if (target == null || target.trim().isEmpty) return;
    final trimmed = target.trim();

    if (trimmed.startsWith('http')) {
      final uri = Uri.tryParse(trimmed);
      if (uri != null) {
        final opened =
            await launchUrl(uri, mode: LaunchMode.externalApplication);
        if (!opened) {
          await launchUrl(uri, mode: LaunchMode.inAppBrowserView);
        }
      }
      return;
    }

    final path = trimmed.startsWith('/') ? trimmed : '/$trimmed';
    if (!context.mounted) return;
    context.push(path);
  }

  Widget _homeSkeleton(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final base = isDark ? DmColors.surfaceAltDark : const Color(0xFFE6E8EC);
    final highlight = isDark ? DmColors.surfaceDark : Colors.white;

    Widget shimmerBox(
        {double height = 16,
        double width = double.infinity,
        BorderRadius? radius}) {
      return Shimmer.fromColors(
        baseColor: base,
        highlightColor: highlight,
        child: Container(
          height: height,
          width: width,
          decoration: BoxDecoration(
            color: base,
            borderRadius: radius ?? DmRadius.r16,
          ),
        ),
      );
    }

    return CustomScrollView(
      physics: const BouncingScrollPhysics(),
      slivers: [
        SliverToBoxAdapter(
          child: shimmerBox(
              height: 240,
              radius: const BorderRadius.only(
                  bottomLeft: Radius.circular(28),
                  bottomRight: Radius.circular(28))),
        ),
        SliverPadding(
          padding: const EdgeInsets.symmetric(
              horizontal: DmSpacing.xxl, vertical: 18),
          sliver: SliverToBoxAdapter(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                shimmerBox(height: 150, radius: DmRadius.r20),
                const SizedBox(height: 20),
                shimmerBox(height: 20, width: 140),
                const SizedBox(height: 12),
                SizedBox(
                  height: 44,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemBuilder: (_, __) => shimmerBox(
                        height: 36, width: 100, radius: DmRadius.r16),
                    separatorBuilder: (_, __) => const SizedBox(width: 6),
                    itemCount: 5,
                  ),
                ),
                const SizedBox(height: 22),
                shimmerBox(height: 20, width: 160),
                const SizedBox(height: 12),
                SizedBox(
                  height: 220,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemBuilder: (_, __) => shimmerBox(
                        height: 220, width: 180, radius: DmRadius.r16),
                    separatorBuilder: (_, __) => const SizedBox(width: 12),
                    itemCount: 3,
                  ),
                ),
              ],
            ),
          ),
        ),
        SliverPadding(
          padding: const EdgeInsets.symmetric(horizontal: DmSpacing.xxl),
          sliver: SliverGrid(
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 2,
              crossAxisSpacing: 12,
              mainAxisSpacing: 12,
              childAspectRatio: 0.72,
            ),
            delegate: SliverChildBuilderDelegate(
              (context, index) => shimmerBox(height: 220, radius: DmRadius.r16),
              childCount: 4,
            ),
          ),
        ),
      ],
    );
  }

  Widget _categoryTab(BuildContext context, CategoryDto category,
      {required bool isEn}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final name = isEn && category.nameEn != null && category.nameEn!.isNotEmpty
        ? category.nameEn!
        : category.name;
    return InkWell(
      onTap: () => context.push('/categories/${category.id}'),
      borderRadius: BorderRadius.circular(18),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
        decoration: BoxDecoration(
          color: isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: DmColors.border(isDark),
          ),
        ),
        child: Text(
          name,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                fontWeight: FontWeight.w600,
                color: DmColors.iconFg(isDark),
              ),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }

  Widget _promoCard(BuildContext context, ProductDto product) {
    return RepaintBoundary(
      child: SizedBox(
        width: double.infinity,
        child: InkWell(
          onTap: () => context.push('/product/${product.id}'),
          child: DmCard(
            margin: EdgeInsets.zero,
            padding: const EdgeInsets.all(12),
            boxShadowOverride: const [],
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Stack(
                    children: [
                      DmNetworkImage(
                        url: product.mainImageUrl,
                        width: double.infinity,
                        fit: BoxFit.cover,
                        borderRadius: DmRadius.r16,
                        cacheWidth: 720,
                        cacheHeight: 420,
                      ),
                      if (product.hasActivePromotion)
                        Positioned(
                          top: 8,
                          left: 8,
                          child: DmBadgePromo(
                              label:
                                  '-${(product.promotionPercent ?? 0).toStringAsFixed(0)}%'),
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Text(product.name,
                    style: Theme.of(context).textTheme.bodyMedium,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis),
                const SizedBox(height: 4),
                Row(
                  children: [
                    if (product.shopRating != null) ...[
                      const Icon(Icons.star,
                          size: 14, color: DmColors.warningLight),
                      const SizedBox(width: 4),
                      Text(product.shopRating!.toStringAsFixed(1),
                          style: Theme.of(context).textTheme.bodySmall),
                      if ((product.shopReviewCount ?? 0) > 0) ...[
                        const SizedBox(width: 4),
                        Text('(${product.shopReviewCount})',
                            style: Theme.of(context).textTheme.bodySmall),
                      ],
                    ],
                    const Spacer(),
                    Text(
                      '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
                      style: Theme.of(context)
                          .textTheme
                          .headlineSmall
                          ?.copyWith(fontSize: 16),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _productCard(BuildContext context, ProductDto product) {
    final strings = ref.read(stringsProvider);
    final adding = _addingToCartIds.contains(product.id);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return RepaintBoundary(
      child: InkWell(
        onTap: () => context.push('/product/${product.id}'),
        child: DmCard(
          margin: EdgeInsets.zero,
          padding: const EdgeInsets.all(12),
          boxShadowOverride: const [],
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Stack(
                  children: [
                    DmNetworkImage(
                      url: product.mainImageUrl,
                      width: double.infinity,
                      fit: BoxFit.cover,
                      borderRadius: DmRadius.r16,
                      cacheWidth: 360,
                      cacheHeight: 520,
                    ),
                    if (product.hasActivePromotion)
                      Positioned(
                        top: 8,
                        left: 8,
                        child: DmBadgePromo(
                            label:
                                '-${(product.promotionPercent ?? 0).toStringAsFixed(0)}%'),
                      ),
                    Positioned(
                      bottom: 8,
                      right: 8,
                      child: Material(
                        color: Colors.transparent,
                        child: InkWell(
                          borderRadius: DmRadius.r12,
                          onTap: adding
                              ? null
                              : () => _addProductToCart(context, product),
                          child: Ink(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 10, vertical: 6),
                            decoration: BoxDecoration(
                              color: DmColors.doorOrange,
                              borderRadius: DmRadius.r12,
                            ),
                            child: Row(
                              children: [
                                Icon(
                                  adding ? Icons.hourglass_top : Icons.add,
                                  color: Colors.white,
                                  size: 16,
                                ),
                                const SizedBox(width: 4),
                                Text(
                                  adding
                                      ? strings.tr('Ajout...', 'Adding...')
                                      : strings.tr('Ajouter', 'Add'),
                                  style: const TextStyle(
                                      color: Colors.white,
                                      fontSize: 11,
                                      fontWeight: FontWeight.w600),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              Text(product.name,
                  style: Theme.of(context).textTheme.bodyMedium,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis),
              const SizedBox(height: 2),
              Text(product.shopName,
                  style: Theme.of(context).textTheme.bodySmall,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis),
              const SizedBox(height: 6),
              Text(
                '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
                style: Theme.of(context)
                    .textTheme
                    .headlineSmall
                    ?.copyWith(fontSize: 16),
              ),
              if ((product.shopReviewCount ?? 0) > 0) ...[
                const SizedBox(height: 2),
                Text(
                  '${product.shopRating?.toStringAsFixed(1) ?? '-'} (${product.shopReviewCount})',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        fontSize: 10,
                        color: DmColors.mutedText(isDark),
                      ),
                ),
              ],
              if (product.hasActivePromotion)
                Text(
                  '${product.currency} ${product.price.toStringAsFixed(2)}',
                  style: Theme.of(context)
                      .textTheme
                      .bodySmall
                      ?.copyWith(decoration: TextDecoration.lineThrough),
                ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _addProductToCart(
      BuildContext context, ProductDto product) async {
    if (_addingToCartIds.contains(product.id)) {
      return;
    }

    setState(() => _addingToCartIds.add(product.id));
    final strings = ref.read(stringsProvider);
    try {
      await ref.read(cartControllerProvider.notifier).addItem(product.id, 1);
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            strings.tr('Produit ajoute au panier.', 'Product added to cart.'),
          ),
        ),
      );
    } catch (error) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(UserErrorService.message(
            context,
            error,
            fallbackFr: 'Impossible d ajouter ce produit pour le moment.',
            fallbackEn: 'Unable to add this product right now.',
          )),
          action: SnackBarAction(
            label: UserErrorService.retryLabel(context),
            onPressed: () {
              if (!mounted) return;
              unawaited(_addProductToCart(context, product));
            },
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _addingToCartIds.remove(product.id));
      }
    }
  }

  Widget _shopCard(BuildContext context, ShopDto shop) {
    final strings = ref.read(stringsProvider);
    return RepaintBoundary(
      child: SizedBox(
        width: 170,
        child: InkWell(
          onTap: () => context.push('/shops/${shop.id}/products'),
          child: DmCard(
            margin: EdgeInsets.zero,
            padding: const EdgeInsets.all(12),
            boxShadowOverride: const [],
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Stack(
                    children: [
                      DmNetworkImage(
                        url: shop.imageUrl,
                        width: double.infinity,
                        fit: BoxFit.cover,
                        borderRadius: DmRadius.r16,
                        cacheWidth: 320,
                        cacheHeight: 240,
                      ),
                      if (shop.isVerified)
                        Positioned(
                          top: 8,
                          left: 8,
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 8, vertical: 4),
                            decoration: BoxDecoration(
                              color: Colors.white.withAlpha(220),
                              borderRadius: DmRadius.r16,
                            ),
                            child: Text(
                              strings.tr('Verifie', 'Verified'),
                              style: const TextStyle(
                                  color: DmColors.doorBlue, fontSize: 11),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  shop.name,
                  style: Theme.of(context).textTheme.bodyMedium,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    if (shop.rating != null) ...[
                      const Icon(Icons.star,
                          size: 14, color: DmColors.warningLight),
                      const SizedBox(width: 4),
                      Text(
                        shop.rating!.toStringAsFixed(1),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      if (shop.reviewCount != null) ...[
                        const SizedBox(width: 4),
                        Text('(${shop.reviewCount})',
                            style: Theme.of(context).textTheme.bodySmall),
                      ],
                      const SizedBox(width: 6),
                      const Text('|'),
                      const SizedBox(width: 6),
                    ],
                    Expanded(
                      child: Text(
                        shop.city,
                        style: Theme.of(context).textTheme.bodySmall,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _iconButton(
    IconData icon,
    BuildContext context, {
    VoidCallback? onTap,
    int badgeCount = 0,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: DmRadius.r14,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Container(
            height: 36,
            width: 36,
            decoration: BoxDecoration(
                color: Colors.white.withAlpha(36), borderRadius: DmRadius.r14),
            child: Icon(icon, color: Colors.white, size: 20),
          ),
          if (badgeCount > 0)
            Positioned(
              right: -4,
              top: -6,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
                constraints: const BoxConstraints(minWidth: 18),
                decoration: BoxDecoration(
                  color: DmColors.doorOrange,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  badgeCount > 99 ? '99+' : '$badgeCount',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  String _normalizeQuery(String raw) {
    return raw.trim().toLowerCase();
  }

  void _safeAnimateToPage(PageController controller, int page) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !controller.hasClients) return;
      final position = controller.position;
      if (position.isScrollingNotifier.value) {
        return;
      }
      try {
        controller.animateToPage(
          page,
          duration: const Duration(milliseconds: 320),
          curve: Curves.easeOut,
        );
      } catch (_) {}
    });
  }

  List<ProductDto> _filterProducts(List<ProductDto> products, String query) {
    if (query.isEmpty) {
      return products;
    }

    return products.where((product) {
      final name = product.name.toLowerCase();
      final shop = product.shopName.toLowerCase();
      final category = product.categoryName.toLowerCase();
      final categoryEn = (product.categoryNameEn ?? '').toLowerCase();
      return name.contains(query) ||
          shop.contains(query) ||
          category.contains(query) ||
          categoryEn.contains(query);
    }).toList();
  }
}
