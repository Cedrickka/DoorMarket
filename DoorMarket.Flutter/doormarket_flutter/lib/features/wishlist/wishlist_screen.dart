import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/saved_product.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';
import '../cart/cart_provider.dart';

class WishlistScreen extends ConsumerStatefulWidget {
  const WishlistScreen({super.key});

  @override
  ConsumerState<WishlistScreen> createState() => _WishlistScreenState();
}

class _WishlistScreenState extends ConsumerState<WishlistScreen>
    with AutomaticKeepAliveClientMixin {
  bool _loading = true;
  List<SavedProductEntry> _items = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final t = _tr;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Wishlist', 'Wishlist'),
            subtitle: t(context, 'Produits sauvegardes', 'Saved products'),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
            showAction: true,
            actionIcon: Icons.delete_outline,
            onAction: _items.isEmpty ? null : _clearAll,
            showLanguageBadge: false,
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _items.isEmpty
                    ? Padding(
                        padding: const EdgeInsets.all(16),
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
                                          Brightness.dark),
                                  borderRadius: DmRadius.r12,
                                ),
                                child: const Icon(
                                  Icons.favorite_border,
                                  color: DmColors.doorOrange,
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Text(
                                  t(context, 'Wishlist vide',
                                      'Wishlist is empty'),
                                  style: Theme.of(context).textTheme.bodySmall,
                                ),
                              ),
                            ],
                          ),
                        ),
                      )
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: GridView.builder(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
                          cacheExtent: 900,
                          itemCount: _items.length,
                          gridDelegate:
                              const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 2,
                            crossAxisSpacing: 6,
                            mainAxisSpacing: 6,
                            childAspectRatio: 0.76,
                          ),
                          itemBuilder: (context, index) =>
                              _itemCard(_items[index]),
                        ),
                      ),
          ),
        ],
      ),
    );
  }

  @override
  bool get wantKeepAlive => true;

  Widget _itemCard(SavedProductEntry item) {
    return InkWell(
      onTap: () => context.push('/product/${item.productId}'),
      child: DmCard(
        margin: EdgeInsets.zero,
        padding: const EdgeInsets.all(8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Stack(
                children: [
                  Positioned.fill(
                    child: DmNetworkImage(
                      url: item.imageUrl,
                      fit: BoxFit.cover,
                      borderRadius: DmRadius.r12,
                    ),
                  ),
                  Positioned(
                    top: 6,
                    right: 6,
                    child: InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: () => _remove(item.productId),
                      child: Container(
                        height: 28,
                        width: 28,
                        decoration: BoxDecoration(
                          color: Colors.black.withAlpha(110),
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
                    color: DmColors.mutedText(
                        Theme.of(context).brightness == Brightness.dark),
                  ),
            ),
            const SizedBox(height: 4),
            Row(
              children: [
                Expanded(
                  child: Text(
                    '${item.currency} ${item.effectivePrice.toStringAsFixed(2)}',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: DmColors.doorOrange,
                          fontWeight: FontWeight.w700,
                        ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                InkWell(
                  onTap: () => _addToCart(item),
                  borderRadius: DmRadius.r12,
                  child: Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: DmColors.doorOrange,
                      borderRadius: DmRadius.r12,
                    ),
                    child: Text(
                      _tr(context, 'Ajouter', 'Add'),
                      style: const TextStyle(color: Colors.white, fontSize: 11),
                    ),
                  ),
                ),
              ],
            ),
            if (item.hasActivePromotion)
              Text(
                '${item.currency} ${item.price.toStringAsFixed(2)}',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      decoration: TextDecoration.lineThrough,
                    ),
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final items = await ref.read(wishlistStoreProvider).getItems();
      if (!mounted) {
        return;
      }
      setState(() {
        _items = items;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) {
        return;
      }
      setState(() {
        _items = const [];
        _loading = false;
      });
    }
  }

  Future<void> _remove(String productId) async {
    await ref.read(wishlistStoreProvider).remove(productId);
    ref.invalidate(wishlistItemsProvider);
    await _load();
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
          content: Text(_tr(context, 'Produit retire', 'Product removed'))),
    );
  }

  Future<void> _clearAll() async {
    await ref.read(wishlistStoreProvider).clear();
    ref.invalidate(wishlistItemsProvider);
    await _load();
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
          content: Text(_tr(context, 'Wishlist videe', 'Wishlist cleared'))),
    );
  }

  Future<void> _addToCart(SavedProductEntry item) async {
    try {
      await ref
          .read(cartControllerProvider.notifier)
          .addItem(item.productId, 1);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
            content: Text(_tr(context, 'Ajoute au panier', 'Added to cart'))),
      );
    } catch (err) {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(err.toString())),
      );
    }
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
