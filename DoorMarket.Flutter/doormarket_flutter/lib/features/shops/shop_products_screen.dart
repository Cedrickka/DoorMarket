import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';

import '../../core/models/categories.dart';
import '../../core/models/products.dart';
import '../../core/models/shops.dart';
import '../../core/providers.dart';
import '../../core/auth/auth_state.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_badge_promo.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';

final shopProductsQueryProvider =
    StateProvider.family<String?, String>((ref, _) => null);
final shopProductsInStockProvider =
    StateProvider.family<bool, String>((ref, _) => true);
final shopProductsCategoryProvider =
    StateProvider.family<String?, String>((ref, _) => null);
final shopReviewsSortProvider =
    StateProvider.family<String, String>((ref, _) => 'recent');

final shopReviewsSummaryProvider =
    FutureProvider.family<ShopReviewSummaryDto, String>((ref, shopId) async {
  final api = ref.read(shopsApiProvider);
  return api.getReviewsSummary(shopId);
});

final shopReviewsProvider =
    FutureProvider.family<List<ShopReviewDto>, String>((ref, shopId) async {
  final api = ref.read(shopsApiProvider);
  final sort = ref.watch(shopReviewsSortProvider(shopId));
  final page =
      await api.getReviews(shopId, page: 1, pageSize: 20, sort: sort);
  return page.items;
});

final shopInfoProvider =
    FutureProvider.family<ShopDto, String>((ref, id) async {
  final api = ref.read(shopsApiProvider);
  return api.getById(id);
});

final shopCategoriesProvider = FutureProvider<List<CategoryDto>>((ref) async {
  final categoriesApi = ref.read(categoriesApiProvider);
  return categoriesApi.getAll();
});

final shopProductsProvider =
    FutureProvider.family<List<ProductDto>, String>((ref, shopId) async {
  final api = ref.read(productsApiProvider);
  final query = ref.watch(shopProductsQueryProvider(shopId));
  final inStock = ref.watch(shopProductsInStockProvider(shopId));
  final categoryId = ref.watch(shopProductsCategoryProvider(shopId));
  final result = await api.search(ProductQuery(
    shopId: shopId,
    pageSize: 120,
    q: query?.trim().isEmpty == true ? null : query?.trim(),
    inStockOnly: inStock,
    categoryId: categoryId,
  ));
  return result.items;
});

class ShopProductsScreen extends ConsumerStatefulWidget {
  final String shopId;

  const ShopProductsScreen({super.key, required this.shopId});

  @override
  ConsumerState<ShopProductsScreen> createState() => _ShopProductsScreenState();
}

class _ShopProductsScreenState extends ConsumerState<ShopProductsScreen> {
  final _searchController = TextEditingController();
  final _reviewController = TextEditingController();
  final _reviewPhotoController = TextEditingController();
  final _reviewVideoController = TextEditingController();
  int _reviewRating = 5;
  bool _submittingReview = false;
  final Set<String> _votingHelpful = <String>{};

  @override
  void dispose() {
    _searchController.dispose();
    _reviewController.dispose();
    _reviewPhotoController.dispose();
    _reviewVideoController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final products = ref.watch(shopProductsProvider(widget.shopId));
    final shopInfo = ref.watch(shopInfoProvider(widget.shopId));
    final categories = ref.watch(shopCategoriesProvider);
    final reviewSummary = ref.watch(shopReviewsSummaryProvider(widget.shopId));
    final reviews = ref.watch(shopReviewsProvider(widget.shopId));
    final auth = ref.watch(authControllerProvider);
    final inStock = ref.watch(shopProductsInStockProvider(widget.shopId));
    final strings = ref.watch(stringsProvider);
    final locale = Localizations.localeOf(context);
    final isEn = locale.languageCode.toLowerCase() == 'en';

    return Scaffold(
      body: Column(
        children: [
          shopInfo.when(
            loading: () => _shopHeader(context, null),
            error: (_, __) => _shopHeader(context, null),
            data: (shop) => _shopHeader(context, shop),
          ),
          Container(
            color: Theme.of(context).scaffoldBackgroundColor,
            padding: const EdgeInsets.fromLTRB(0, 8, 0, 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DmSearchBar(
                  hint: strings.tr('Rechercher un produit', 'Search a product'),
                  controller: _searchController,
                  onSubmitted: (value) {
                    ref
                        .read(shopProductsQueryProvider(widget.shopId).notifier)
                        .state = value.trim().isEmpty ? null : value.trim();
                    ref.invalidate(shopProductsProvider(widget.shopId));
                  },
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        strings.tr('En stock uniquement', 'In stock only'),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                    Switch.adaptive(
                      value: inStock,
                      onChanged: (value) {
                        ref
                            .read(shopProductsInStockProvider(widget.shopId)
                                .notifier)
                            .state = value;
                        ref.invalidate(shopProductsProvider(widget.shopId));
                      },
                    ),
                  ],
                ),
                categories.when(
                  loading: () => const SizedBox.shrink(),
                  error: (_, __) => const SizedBox.shrink(),
                  data: (items) => SizedBox(
                    height: 38,
                    child: ListView.separated(
                      scrollDirection: Axis.horizontal,
                      itemCount: items.length + 1,
                      separatorBuilder: (_, __) => const SizedBox(width: 6),
                      itemBuilder: (context, index) {
                        if (index == 0) {
                          return _categoryChip(
                            context,
                            strings.tr('Toutes', 'All'),
                            isSelected: ref.watch(shopProductsCategoryProvider(
                                    widget.shopId)) ==
                                null,
                            onTap: () {
                              ref
                                  .read(shopProductsCategoryProvider(
                                          widget.shopId)
                                      .notifier)
                                  .state = null;
                              ref.invalidate(
                                  shopProductsProvider(widget.shopId));
                            },
                          );
                        }
                        final cat = items[index - 1];
                        final name =
                            isEn && cat.nameEn != null && cat.nameEn!.isNotEmpty
                                ? cat.nameEn!
                                : cat.name;
                        return _categoryChip(
                          context,
                          name,
                          isSelected: ref.watch(shopProductsCategoryProvider(
                                  widget.shopId)) ==
                              cat.id,
                          onTap: () {
                            ref
                                .read(
                                    shopProductsCategoryProvider(widget.shopId)
                                        .notifier)
                                .state = cat.id;
                            ref.invalidate(shopProductsProvider(widget.shopId));
                          },
                        );
                      },
                    ),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: CustomScrollView(
              physics: const BouncingScrollPhysics(),
              slivers: [
                products.when(
                  loading: () => _productsSkeleton(context),
                  error: (err, _) => SliverToBoxAdapter(
                      child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Text(strings.tr('Erreur: $err', 'Error: $err')),
                  )),
                  data: (items) {
                    if (items.isEmpty) {
                      return SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.all(10),
                          child: DmCard(
                            margin: EdgeInsets.zero,
                            child: Row(
                              children: [
                                Container(
                                  height: 40,
                                  width: 40,
                                  decoration: BoxDecoration(
                                    color: DmColors.iconBg(
                                      Theme.of(context).brightness ==
                                          Brightness.dark,
                                    ),
                                    borderRadius: DmRadius.r12,
                                  ),
                                  child: const Icon(Icons.inventory_2_outlined,
                                      color: DmColors.doorOrange),
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Text(
                                    strings.tr(
                                      'Aucun produit pour cette boutique',
                                      'No products for this shop',
                                    ),
                                    style:
                                        Theme.of(context).textTheme.bodySmall,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    }
                    return SliverPadding(
                      padding: const EdgeInsets.fromLTRB(0, 0, 0, 10),
                      sliver: SliverGrid(
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 4,
                          crossAxisSpacing: 2,
                          mainAxisSpacing: 2,
                          childAspectRatio: 0.56,
                        ),
                        delegate: SliverChildBuilderDelegate(
                          (context, index) =>
                              _productCard(context, items[index]),
                          childCount: items.length,
                        ),
                      ),
                    );
                  },
                ),
                SliverPadding(
                  padding: const EdgeInsets.fromLTRB(10, 0, 10, DmSpacing.xxxl),
                  sliver: SliverToBoxAdapter(
                    child: _reviewsSection(
                      context,
                      reviewSummary: reviewSummary,
                      reviews: reviews,
                      auth: auth,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _shopHeader(BuildContext context, ShopDto? shop) {
    final strings = ref.read(stringsProvider);
    final name = shop?.name ?? strings.tr('Boutique', 'Shop');
    final city = shop?.city ?? strings.tr('Ville', 'City');
    final country = shop?.countryTag ?? '';
    final subtitle = country.isEmpty ? city : '$city - $country';

    return Stack(
      children: [
        DmNetworkImage(
          url: shop?.imageUrl,
          height: 220,
          width: double.infinity,
          borderRadius: BorderRadius.zero,
        ),
        Positioned.fill(
          child: Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [
                  Colors.black.withAlpha(100),
                  Colors.black.withAlpha(200)
                ],
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
              ),
            ),
          ),
        ),
        Positioned(
          top: MediaQuery.of(context).padding.top + 12,
          left: 16,
          child: _headerIcon(context, Icons.arrow_back,
              () => Navigator.of(context).maybePop()),
        ),
        Positioned(
          bottom: 18,
          left: 24,
          right: 24,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                name,
                style: const TextStyle(
                    color: Colors.white,
                    fontSize: 22,
                    fontWeight: FontWeight.w700),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Text(
                subtitle,
                style: const TextStyle(color: Color(0xFFE2ECFA), fontSize: 12),
              ),
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                children: [
                  _chip(strings.tr('Livraison rapide', 'Fast delivery')),
                  if (shop?.isVerified == true)
                    _chip(strings.tr('Verifiee', 'Verified')),
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _chip(String label) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.white.withAlpha(36),
        borderRadius: DmRadius.r16,
      ),
      child: Text(label,
          style: const TextStyle(
              color: Colors.white, fontSize: 11, fontWeight: FontWeight.w600)),
    );
  }

  Widget _categoryChip(BuildContext context, String label,
      {required bool isSelected, required VoidCallback onTap}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isSelected ? DmColors.doorBlue : DmColors.altSurface(isDark);
    final border = isSelected ? DmColors.doorBlue : DmColors.border(isDark);
    final textColor = isSelected
        ? Colors.white
        : (DmColors.iconFg(isDark));

    return InkWell(
      onTap: onTap,
      borderRadius: DmRadius.r16,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: DmRadius.r16,
          border: Border.all(color: border),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: textColor,
            fontWeight: FontWeight.w600,
            fontSize: 11,
          ),
        ),
      ),
    );
  }

  Widget _productCard(BuildContext context, ProductDto product) {
    return InkWell(
      onTap: () => context.push('/product/${product.id}'),
      child: DmCard(
        margin: EdgeInsets.zero,
        padding: const EdgeInsets.all(4),
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
                    borderRadius: DmRadius.r12,
                  ),
                  if (product.hasActivePromotion)
                    Positioned(
                      top: 4,
                      left: 4,
                      child: DmBadgePromo(
                        label:
                            '-${(product.promotionPercent ?? 0).toStringAsFixed(0)}%',
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 4),
            Text(
              product.name,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    fontSize: 11,
                  ),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            Text(
              '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    fontSize: 11,
                    color: DmColors.doorOrange,
                    fontWeight: FontWeight.w700,
                  ),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }

  SliverGrid _productsSkeleton(BuildContext context) {
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
            borderRadius: radius ?? DmRadius.r12,
          ),
        ),
      );
    }

    return SliverGrid(
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 4,
        crossAxisSpacing: 2,
        mainAxisSpacing: 2,
        childAspectRatio: 0.56,
      ),
      delegate: SliverChildBuilderDelegate(
        (context, index) => DmCard(
          margin: EdgeInsets.zero,
          padding: const EdgeInsets.all(4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: shimmerBox(
                    height: double.infinity,
                    width: double.infinity,
                    radius: DmRadius.r12),
              ),
              const SizedBox(height: 4),
              shimmerBox(height: 10, width: 90),
              const SizedBox(height: 4),
              shimmerBox(height: 10, width: 60),
            ],
          ),
        ),
        childCount: 6,
      ),
    );
  }

  Widget _reviewsSection(
    BuildContext context, {
    required AsyncValue<ShopReviewSummaryDto> reviewSummary,
    required AsyncValue<List<ShopReviewDto>> reviews,
    required AuthState auth,
  }) {
    final strings = ref.read(stringsProvider);
    final role = (auth.me?.role ?? '').toLowerCase();
    final canWrite = auth.isAuthenticated && (role == 'client' || role == '1');

    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  strings.tr('Avis clients', 'Customer reviews'),
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              SizedBox(
                width: 190,
                child: DropdownButtonFormField<String>(
                  initialValue:
                      ref.watch(shopReviewsSortProvider(widget.shopId)),
                  decoration: InputDecoration(
                    labelText: strings.tr('Tri', 'Sort'),
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 10),
                    border: const OutlineInputBorder(),
                  ),
                  items: [
                    DropdownMenuItem(
                      value: 'recent',
                      child: Text(strings.tr('Recents', 'Recent')),
                    ),
                    DropdownMenuItem(
                      value: 'verified_purchase',
                      child: Text(strings.tr('Achats verifies', 'Verified')),
                    ),
                    DropdownMenuItem(
                      value: 'rating_desc',
                      child: Text(strings.tr('Note decroissante', 'Top rated')),
                    ),
                    DropdownMenuItem(
                      value: 'rating_asc',
                      child: Text(strings.tr('Note croissante', 'Low rated')),
                    ),
                  ],
                  onChanged: (value) {
                    if (value == null || value.isEmpty) {
                      return;
                    }
                    ref
                        .read(shopReviewsSortProvider(widget.shopId).notifier)
                        .state = value;
                    ref.invalidate(shopReviewsProvider(widget.shopId));
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          reviewSummary.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (err, _) => Text(strings.tr('Erreur: $err', 'Error: $err')),
            data: (summary) => Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: DmColors.iconBg(
                    Theme.of(context).brightness == Brightness.dark),
                borderRadius: DmRadius.r12,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    summary.reviewCount == 0
                        ? strings.tr(
                            'Aucun avis pour le moment', 'No reviews yet')
                        : '${summary.averageRating?.toStringAsFixed(2) ?? '0.00'} / 5  (${summary.reviewCount})',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  if (summary.reviewCount > 0) ...[
                    const SizedBox(height: 4),
                    Text(
                      '5*: ${summary.count5} | 4*: ${summary.count4} | 3*: ${summary.count3} | 2*: ${summary.count2} | 1*: ${summary.count1}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ],
              ),
            ),
          ),
          if (canWrite) ...[
            const SizedBox(height: 12),
            Text(
              strings.tr('Laisser un avis', 'Leave a review'),
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: DropdownButtonFormField<int>(
                    initialValue: _reviewRating,
                    decoration: InputDecoration(
                      labelText: strings.tr('Note', 'Rating'),
                    ),
                    items: [5, 4, 3, 2, 1]
                        .map(
                          (value) => DropdownMenuItem<int>(
                            value: value,
                            child: Text('$value / 5'),
                          ),
                        )
                        .toList(),
                    onChanged: (value) {
                      if (value == null) {
                        return;
                      }
                      setState(() => _reviewRating = value);
                    },
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _reviewController,
              maxLines: 3,
              maxLength: 1000,
              decoration: InputDecoration(
                hintText:
                    strings.tr('Commentaire (optionnel)', 'Comment (optional)'),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _reviewPhotoController,
              decoration: InputDecoration(
                hintText: strings.tr(
                  'URL photo (optionnel, separez par virgule)',
                  'Photo URL (optional, comma separated)',
                ),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _reviewVideoController,
              decoration: InputDecoration(
                hintText: strings.tr(
                  'URL video (optionnel, separez par virgule)',
                  'Video URL (optional, comma separated)',
                ),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: _submittingReview ? null : _submitReview,
                child: Text(
                  _submittingReview
                      ? strings.tr('Publication...', 'Submitting...')
                      : strings.tr('Publier', 'Submit'),
                ),
              ),
            ),
          ],
          const SizedBox(height: 8),
          reviews.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (err, _) => Text(strings.tr('Erreur: $err', 'Error: $err')),
            data: (items) {
              if (items.isEmpty) {
                return Text(
                  strings.tr('Aucun avis pour cette boutique',
                      'No reviews for this shop'),
                  style: Theme.of(context).textTheme.bodySmall,
                );
              }

              return Column(
                children: items.map((item) {
                  final reviewer = _displayReviewer(item.userEmail);
                  final createdAt = item.createdAtUtc.toLocal();
                  return Container(
                    width: double.infinity,
                    margin: const EdgeInsets.only(top: 8),
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      borderRadius: DmRadius.r12,
                      border: Border.all(
                        color: DmColors.border(
                            Theme.of(context).brightness == Brightness.dark),
                      ),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    reviewer,
                                    style:
                                        Theme.of(context).textTheme.bodyMedium,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                  if (item.isVerifiedPurchase) ...[
                                    const SizedBox(height: 2),
                                    Text(
                                      strings.tr(
                                          'Achat verifie', 'Verified purchase'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodySmall
                                          ?.copyWith(
                                            color:
                                                Theme.of(context).brightness ==
                                                        Brightness.dark
                                                    ? DmColors.successDark
                                                    : DmColors.successLight,
                                            fontWeight: FontWeight.w600,
                                          ),
                                    ),
                                  ],
                                ],
                              ),
                            ),
                            const SizedBox(width: 8),
                            Text(
                              '${item.rating}/5',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${createdAt.day.toString().padLeft(2, '0')}/${createdAt.month.toString().padLeft(2, '0')}/${createdAt.year} ${createdAt.hour.toString().padLeft(2, '0')}:${createdAt.minute.toString().padLeft(2, '0')}',
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                        if ((item.comment ?? '').trim().isNotEmpty) ...[
                          const SizedBox(height: 6),
                          Text(item.comment!.trim()),
                        ],
                        if (item.photoUrls.isNotEmpty) ...[
                          const SizedBox(height: 8),
                          SizedBox(
                            height: 66,
                            child: ListView.separated(
                              scrollDirection: Axis.horizontal,
                              itemCount: item.photoUrls.length,
                              separatorBuilder: (_, __) =>
                                  const SizedBox(width: 8),
                              itemBuilder: (ctx, photoIndex) => ClipRRect(
                                borderRadius: DmRadius.r10,
                                child: DmNetworkImage(
                                  url: item.photoUrls[photoIndex],
                                  width: 66,
                                  height: 66,
                                  fit: BoxFit.cover,
                                ),
                              ),
                            ),
                          ),
                        ],
                        if (item.videoUrls.isNotEmpty) ...[
                          const SizedBox(height: 8),
                          Wrap(
                            spacing: 6,
                            runSpacing: 6,
                            children: item.videoUrls
                                .map(
                                  (video) => Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 8, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: Theme.of(context).brightness ==
                                              Brightness.dark
                                          ? DmColors.surfaceAltDark
                                          : DmColors.iconBgLight,
                                      borderRadius: DmRadius.r12,
                                    ),
                                    child: Text(
                                      strings.tr('Video', 'Video'),
                                      style: const TextStyle(fontSize: 12),
                                    ),
                                  ),
                                )
                                .toList(),
                          ),
                        ],
                        const SizedBox(height: 8),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: OutlinedButton.icon(
                            onPressed: _votingHelpful.contains(item.id)
                                ? null
                                : () => _voteHelpful(item),
                            icon: Icon(
                              item.isHelpfulByCurrentUser
                                  ? Icons.thumb_up
                                  : Icons.thumb_up_outlined,
                              size: 16,
                            ),
                            label: Text(
                              '${strings.tr('Utile', 'Helpful')} (${item.helpfulCount})',
                            ),
                          ),
                        ),
                      ],
                    ),
                  );
                }).toList(),
              );
            },
          ),
        ],
      ),
    );
  }

  Future<void> _submitReview() async {
    final strings = ref.read(stringsProvider);
    setState(() => _submittingReview = true);
    try {
      await ref.read(shopsApiProvider).upsertReview(
            widget.shopId,
            CreateShopReviewRequest(
              rating: _reviewRating,
              comment: _reviewController.text.trim().isEmpty
                  ? null
                  : _reviewController.text.trim(),
              orderId: null,
              photoUrls: _parseCsvUrls(_reviewPhotoController.text),
              videoUrls: _parseCsvUrls(_reviewVideoController.text),
            ),
          );
      _reviewController.clear();
      _reviewPhotoController.clear();
      _reviewVideoController.clear();
      setState(() => _reviewRating = 5);
      ref.invalidate(shopReviewsSummaryProvider(widget.shopId));
      ref.invalidate(shopReviewsProvider(widget.shopId));
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
            content: Text(strings.tr('Avis enregistre', 'Review submitted'))),
      );
    } catch (err) {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(err.toString())),
      );
    } finally {
      if (mounted) {
        setState(() => _submittingReview = false);
      }
    }
  }

  Future<void> _voteHelpful(ShopReviewDto review) async {
    final strings = ref.read(stringsProvider);
    setState(() => _votingHelpful.add(review.id));
    try {
      await ref.read(shopsApiProvider).voteHelpful(
            widget.shopId,
            review.id,
            isHelpful: !review.isHelpfulByCurrentUser,
          );
      ref.invalidate(shopReviewsProvider(widget.shopId));
    } catch (err) {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            strings.tr('Vote impossible: $err', 'Vote failed: $err'),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _votingHelpful.remove(review.id));
      }
    }
  }

  List<String>? _parseCsvUrls(String raw) {
    final parsed = raw
        .split(',')
        .map((e) => e.trim())
        .where((e) => e.isNotEmpty)
        .toList(growable: false);
    return parsed.isEmpty ? null : parsed;
  }

  String _displayReviewer(String? email) {
    if (email == null || email.trim().isEmpty) {
      return _tr(context, 'Client', 'Customer');
    }

    final value = email.trim();
    final atIndex = value.indexOf('@');
    if (atIndex <= 0) {
      return value;
    }
    return value.substring(0, atIndex);
  }

  Widget _headerIcon(BuildContext context, IconData icon, VoidCallback onTap) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return InkWell(
      onTap: onTap,
      borderRadius: DmRadius.r12,
      child: Container(
        height: 40,
        width: 40,
        decoration: BoxDecoration(
          color: isDark ? const Color(0x44FFFFFF) : Colors.white.withAlpha(200),
          borderRadius: DmRadius.r12,
          border: Border.all(color: const Color(0x1FFFFFFF)),
        ),
        child: Icon(
          icon,
          color: DmColors.iconFg(isDark),
          size: 18,
        ),
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}

