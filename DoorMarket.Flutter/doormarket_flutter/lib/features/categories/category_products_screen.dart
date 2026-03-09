import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/products.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';

final categoryProductsQueryProvider =
    StateProvider.family<String?, String>((ref, _) => null);
final categoryProductsInStockProvider =
    StateProvider.family<bool, String>((ref, _) => true);

final categoryProductsProvider =
    FutureProvider.family<List<ProductDto>, String>((ref, categoryId) async {
  final api = ref.read(productsApiProvider);
  final query = ref.watch(categoryProductsQueryProvider(categoryId));
  final inStock = ref.watch(categoryProductsInStockProvider(categoryId));
  final result = await api.search(ProductQuery(
    categoryId: categoryId,
    pageSize: 40,
    q: query?.trim().isEmpty == true ? null : query?.trim(),
    inStockOnly: inStock,
  ));
  return result.items;
});

class CategoryProductsScreen extends ConsumerStatefulWidget {
  final String categoryId;

  const CategoryProductsScreen({super.key, required this.categoryId});

  @override
  ConsumerState<CategoryProductsScreen> createState() =>
      _CategoryProductsScreenState();
}

class _CategoryProductsScreenState
    extends ConsumerState<CategoryProductsScreen> {
  final _searchController = TextEditingController();
  Timer? _searchDebounce;

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final data = ref.watch(categoryProductsProvider(widget.categoryId));
    final inStock =
        ref.watch(categoryProductsInStockProvider(widget.categoryId));

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Produits', 'Products'),
            subtitle:
                t(context, 'Selection de la categorie', 'Category selection'),
            showBack: true,
            onBack: () => Navigator.of(context).pop(),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              0,
              DmSpacing.md,
              0,
              DmSpacing.md,
            ),
            child: Column(
              children: [
                DmSearchBar(
                  hint: t(context, 'Rechercher dans la categorie',
                      'Search in this category'),
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  onSubmitted: _applySearchQuery,
                  trailingIcon: Icons.clear,
                  onTrailingTap: () {
                    _searchController.clear();
                    _applySearchQuery('');
                  },
                ),
                const SizedBox(height: 8),
                DmCard(
                  margin: EdgeInsets.zero,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  boxShadowOverride: const [],
                  child: SizedBox(
                    height: 34,
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            t(context, 'En stock uniquement', 'In stock only'),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                        ),
                        Transform.scale(
                          scale: 0.84,
                          child: Switch.adaptive(
                            value: inStock,
                            onChanged: (value) {
                              ref
                                  .read(categoryProductsInStockProvider(
                                          widget.categoryId)
                                      .notifier)
                                  .state = value;
                              ref.invalidate(
                                  categoryProductsProvider(widget.categoryId));
                            },
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: data.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (err, _) => Center(
                  child: Text(t(context, 'Erreur: $err', 'Error: $err'))),
              data: (products) => RefreshIndicator(
                onRefresh: _refreshProducts,
                child: products.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        children: [
                          const SizedBox(height: 28),
                          Center(
                            child: Text(
                              t(
                                context,
                                'Aucun produit pour cette categorie',
                                'No product for this category',
                              ),
                            ),
                          ),
                        ],
                      )
                    : GridView.builder(
                        padding: const EdgeInsets.fromLTRB(
                          0,
                          DmSpacing.sm,
                          0,
                          DmSpacing.xxl,
                        ),
                        physics: const AlwaysScrollableScrollPhysics(),
                        cacheExtent: 900,
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 4,
                          crossAxisSpacing: 4,
                          mainAxisSpacing: 4,
                          childAspectRatio: 0.52,
                        ),
                        itemCount: products.length,
                        itemBuilder: (context, index) =>
                            _productCard(context, products[index]),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _refreshProducts() async {
    ref.invalidate(categoryProductsProvider(widget.categoryId));
    await ref.read(categoryProductsProvider(widget.categoryId).future);
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(
      const Duration(milliseconds: 220),
      () => _applySearchQuery(value),
    );
  }

  void _applySearchQuery(String value) {
    ref.read(categoryProductsQueryProvider(widget.categoryId).notifier).state =
        value.trim().isEmpty ? null : value.trim();
    ref.invalidate(categoryProductsProvider(widget.categoryId));
  }

  Widget _productCard(BuildContext context, ProductDto product) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return InkWell(
      onTap: () => context.push('/product/${product.id}'),
      child: DmCard(
        margin: EdgeInsets.zero,
        padding: const EdgeInsets.all(6),
        boxShadowOverride: const [],
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: DmNetworkImage(
                url: product.mainImageUrl,
                width: double.infinity,
                fit: BoxFit.cover,
                borderRadius: DmRadius.r12,
                cacheWidth: 260,
                cacheHeight: 340,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              product.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodySmall,
            ),
            Text(
              '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: DmColors.doorOrange,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            if (product.hasActivePromotion)
              Text(
                '${product.currency} ${product.price.toStringAsFixed(2)}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      fontSize: 10,
                      decoration: TextDecoration.lineThrough,
                      color: DmColors.mutedText(isDark),
                    ),
              ),
          ],
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
