import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shimmer/shimmer.dart';

import '../../core/models/products.dart';
import '../../core/models/saved_product.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_badge_promo.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../cart/cart_provider.dart';
import 'product_provider.dart';

class ProductScreen extends ConsumerStatefulWidget {
  final String productId;

  const ProductScreen({super.key, required this.productId});

  @override
  ConsumerState<ProductScreen> createState() => _ProductScreenState();
}

class _ProductScreenState extends ConsumerState<ProductScreen> {
  int qty = 1;
  bool _adding = false;
  bool _isFavorite = false;
  bool _favoriteBusy = false;
  String? _syncedProductId;

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final data = ref.watch(productProvider(widget.productId));

    return Scaffold(
      body: data.when(
        loading: () => _loadingSkeleton(context),
        error: (err, _) =>
            Center(child: Text(t(context, 'Erreur: $err', 'Error: $err'))),
        data: (product) => _buildContent(context, product),
      ),
    );
  }

  Widget _buildContent(BuildContext context, ProductDto product) {
    final t = _tr;
    final total = product.effectivePrice * qty;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    _scheduleTrackAndSync(product);

    return SingleChildScrollView(
      padding: const EdgeInsets.only(bottom: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Stack(
            children: [
              DmNetworkImage(
                url: product.mainImageUrl,
                height: 280,
                width: double.infinity,
                fit: BoxFit.cover,
                borderRadius: BorderRadius.zero,
              ),
              if (product.hasActivePromotion)
                Positioned(
                  top: 12,
                  left: 12,
                  child: DmBadgePromo(
                      label:
                          '-${(product.promotionPercent ?? 0).toStringAsFixed(0)}%'),
                ),
              Positioned(
                top: MediaQuery.of(context).padding.top + 12,
                left: 16,
                child: _headerIcon(context, Icons.arrow_back,
                    () => Navigator.of(context).pop()),
              ),
              Positioned(
                top: MediaQuery.of(context).padding.top + 12,
                right: 16,
                child: Row(
                  children: [
                    _headerIcon(context, Icons.search, () {}),
                    const SizedBox(width: 10),
                    _headerIcon(
                      context,
                      _isFavorite ? Icons.favorite : Icons.favorite_border,
                      _favoriteBusy ? null : () => _toggleWishlist(product),
                    ),
                  ],
                ),
              ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    if ((product.shopReviewCount ?? 0) > 0) ...[
                      Icon(Icons.star,
                          color: isDark
                              ? DmColors.warningDark
                              : DmColors.warningLight,
                          size: 18),
                      const SizedBox(width: 4),
                      Text(
                          '${product.shopRating?.toStringAsFixed(1) ?? '0.0'} (${product.shopReviewCount} ${t(context, 'avis', 'reviews')})',
                          style: Theme.of(context).textTheme.bodySmall),
                    ] else
                      Text(
                        t(context, 'Aucun avis', 'No reviews'),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    const Spacer(),
                    Text(
                      product.shopName,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: isDark
                                ? DmColors.textSecondaryDark
                                : DmColors.doorBlue,
                          ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(product.name,
                    style: Theme.of(context).textTheme.headlineMedium),
                const SizedBox(height: 4),
                Text(product.categoryName,
                    style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 8),
                Text(
                    product.description ??
                        t(context, 'Aucune description', 'No description'),
                    style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 14),
                Row(
                  children: [
                    Text(
                      '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
                      style: Theme.of(context)
                          .textTheme
                          .headlineLarge
                          ?.copyWith(color: DmColors.doorOrange),
                    ),
                    const SizedBox(width: 8),
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
                const SizedBox(height: 18),
                DmPrimaryButton(
                  label: t(context, 'Voir la boutique', 'View shop'),
                  onPressed: () =>
                      context.push('/shops/${product.shopId}/products'),
                ),
                const SizedBox(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(t(context, 'Quantite', 'Quantity'),
                        style: Theme.of(context).textTheme.bodyMedium),
                    Row(
                      children: [
                        _qtyButton(context, Icons.remove,
                            () => setState(() => qty = qty > 1 ? qty - 1 : 1)),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                          child: Text('$qty',
                              style: Theme.of(context).textTheme.headlineSmall),
                        ),
                        _qtyButton(
                            context, Icons.add, () => setState(() => qty += 1)),
                      ],
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(t(context, 'Total', 'Total'),
                        style: Theme.of(context).textTheme.bodyMedium),
                    Text(
                      '${product.currency} ${total.toStringAsFixed(2)}',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ],
                ),
                const SizedBox(height: 18),
                DmPrimaryButton(
                  label: _adding
                      ? t(context, 'Ajout...', 'Adding...')
                      : t(context, 'Ajouter au panier', 'Add to cart'),
                  onPressed: _adding ? null : () => _addToCart(product),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _loadingSkeleton(BuildContext context) {
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

    return SingleChildScrollView(
      padding: const EdgeInsets.only(bottom: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          shimmerBox(height: 280, radius: BorderRadius.zero),
          Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                shimmerBox(height: 14, width: 140),
                const SizedBox(height: 12),
                shimmerBox(height: 22, width: 220),
                const SizedBox(height: 8),
                shimmerBox(height: 14, width: 160),
                const SizedBox(height: 12),
                shimmerBox(height: 14),
                const SizedBox(height: 8),
                shimmerBox(height: 14, width: 260),
                const SizedBox(height: 16),
                shimmerBox(height: 42, radius: DmRadius.r14),
                const SizedBox(height: 18),
                shimmerBox(height: 20, width: 120),
                const SizedBox(height: 10),
                shimmerBox(height: 36, width: 140, radius: DmRadius.r12),
                const SizedBox(height: 16),
                shimmerBox(height: 20, width: 100),
                const SizedBox(height: 12),
                shimmerBox(height: 52, radius: DmRadius.r14),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _qtyButton(BuildContext context, IconData icon, VoidCallback onTap) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final iconColor = DmColors.iconFg(isDark);
    return InkWell(
      onTap: onTap,
      borderRadius: DmRadius.r12,
      child: Container(
        height: 36,
        width: 36,
        decoration: BoxDecoration(
          color: isDark ? DmColors.iconBgDark : DmColors.iconBgLight,
          borderRadius: DmRadius.r12,
        ),
        child: Icon(icon, size: 18, color: iconColor),
      ),
    );
  }

  Widget _headerIcon(BuildContext context, IconData icon, VoidCallback? onTap) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final iconColor = isDark ? Colors.white : DmColors.doorBlue;

    return InkWell(
      onTap: onTap,
      borderRadius: DmRadius.r12,
      child: Container(
        height: 40,
        width: 40,
        decoration: BoxDecoration(
          color: isDark ? const Color(0x44FFFFFF) : Colors.white.withAlpha(217),
          borderRadius: DmRadius.r12,
          border: Border.all(color: const Color(0x1FFFFFFF)),
        ),
        child: Icon(
          icon,
          color: onTap == null ? iconColor.withAlpha(130) : iconColor,
          size: 18,
        ),
      ),
    );
  }

  Future<void> _addToCart(ProductDto product) async {
    setState(() => _adding = true);
    try {
      await ref.read(cartControllerProvider.notifier).addItem(product.id, qty);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(_tr(context, 'Ajoute au panier', 'Added to cart'))),
        );
      }
    } catch (_) {}
    if (mounted) setState(() => _adding = false);
  }

  void _scheduleTrackAndSync(ProductDto product) {
    if (_syncedProductId == product.id) {
      return;
    }

    _syncedProductId = product.id;
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      try {
        await ref
            .read(recentlyViewedStoreProvider)
            .track(_toSavedEntry(product));
        ref.invalidate(recentlyViewedItemsProvider);
        final inWishlist =
            await ref.read(wishlistStoreProvider).contains(product.id);
        if (mounted) {
          setState(() => _isFavorite = inWishlist);
        }
      } catch (_) {}
    });
  }

  Future<void> _toggleWishlist(ProductDto product) async {
    setState(() => _favoriteBusy = true);
    try {
      final nowFavorite =
          await ref.read(wishlistStoreProvider).toggle(_toSavedEntry(product));
      ref.invalidate(wishlistItemsProvider);
      if (!mounted) {
        return;
      }
      setState(() => _isFavorite = nowFavorite);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(_tr(
            context,
            nowFavorite ? 'Ajoute a la wishlist' : 'Retire de la wishlist',
            nowFavorite ? 'Added to wishlist' : 'Removed from wishlist',
          )),
        ),
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
        setState(() => _favoriteBusy = false);
      }
    }
  }

  SavedProductEntry _toSavedEntry(ProductDto product) {
    return SavedProductEntry(
      productId: product.id,
      shopId: product.shopId,
      shopName: product.shopName,
      productName: product.name,
      imageUrl: product.mainImageUrl,
      price: product.price,
      effectivePrice: product.effectivePrice,
      hasActivePromotion: product.hasActivePromotion,
      currency: product.currency,
      savedAtUtc: DateTime.now().toUtc(),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}

