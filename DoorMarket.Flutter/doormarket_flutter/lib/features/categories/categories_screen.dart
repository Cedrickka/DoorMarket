import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/categories.dart';
import '../../core/models/products.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_badge_promo.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';
import 'categories_provider.dart';

class CategoriesScreen extends ConsumerStatefulWidget {
  const CategoriesScreen({super.key});

  @override
  ConsumerState<CategoriesScreen> createState() => _CategoriesScreenState();
}

class _CategoriesScreenState extends ConsumerState<CategoriesScreen>
    with AutomaticKeepAliveClientMixin {
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
    super.build(context);
    final data = ref.watch(categoriesDataProvider);
    final strings = ref.watch(stringsProvider);
    final locale = Localizations.localeOf(context);
    final isEn = locale.languageCode.toLowerCase() == 'en';
    final query = ref.watch(categoriesSearchQueryProvider);
    final selectedCategoryId = ref.watch(categoriesSelectedCategoryIdProvider);

    return Scaffold(
      body: data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) =>
            Center(child: Text(strings.tr('Erreur: $err', 'Error: $err'))),
        data: (value) {
          final filteredProducts = _filterProducts(
            value.products,
            query: query,
            selectedCategoryId: selectedCategoryId,
          );

          return Column(
            children: [
              DmHeader(
                title: strings.tr('Categories', 'Categories'),
                subtitle: strings.tr('Filtrez rapidement les produits.',
                    'Filter products instantly.'),
                showBack: false,
                showAction: true,
                actionIcon: Icons.person_outline,
                onAction: () => context.push('/profile'),
              ),
              Container(
                padding: const EdgeInsets.only(top: 8, bottom: 8),
                color: Theme.of(context).brightness == Brightness.dark
                    ? DmColors.surfaceDark
                    : DmColors.surfaceLight,
                child: DmSearchBar(
                  hint: strings.tr(
                      'Rechercher dans les produits', 'Search in products'),
                  controller: _searchController,
                  onChanged: _onSearchChanged,
                  onSubmitted: _applySearchQuery,
                  trailingIcon: Icons.clear,
                  onTrailingTap: _clearSearch,
                ),
              ),
              const SizedBox(height: 10),
              Expanded(
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    final leftWidth = (constraints.maxWidth / 4)
                        .clamp(92.0, 126.0)
                        .toDouble();
                    return Row(
                      children: [
                        Container(
                          width: leftWidth,
                          decoration: BoxDecoration(
                            border: Border(
                              right: BorderSide(
                                color: Theme.of(context).brightness ==
                                        Brightness.dark
                                    ? DmColors.borderDark
                                    : DmColors.borderLight,
                              ),
                            ),
                          ),
                          child: _categoriesPanel(
                            context,
                            value.categories,
                            selectedCategoryId: selectedCategoryId,
                            isEn: isEn,
                          ),
                        ),
                        Expanded(
                          child: RefreshIndicator(
                            onRefresh: _refreshData,
                            child: _productsGrid(context, filteredProducts),
                          ),
                        ),
                      ],
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  @override
  bool get wantKeepAlive => true;

  List<ProductDto> _filterProducts(
    List<ProductDto> products, {
    required String query,
    required String? selectedCategoryId,
  }) {
    final normalized = query.trim().toLowerCase();
    return products.where((product) {
      if (selectedCategoryId != null &&
          product.categoryId != selectedCategoryId) {
        return false;
      }

      if (normalized.isEmpty) {
        return true;
      }

      final name = product.name.toLowerCase();
      final shop = product.shopName.toLowerCase();
      final category = product.categoryName.toLowerCase();
      final categoryEn = (product.categoryNameEn ?? '').toLowerCase();
      return name.contains(normalized) ||
          shop.contains(normalized) ||
          category.contains(normalized) ||
          categoryEn.contains(normalized);
    }).toList();
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 220), () {
      _applySearchQuery(value);
    });
  }

  void _applySearchQuery(String value) {
    ref.read(categoriesSearchQueryProvider.notifier).state = value.trim();
  }

  void _clearSearch() {
    _searchController.clear();
    _applySearchQuery('');
  }

  Future<void> _refreshData() async {
    ref.invalidate(categoriesDataProvider);
    await ref.read(categoriesDataProvider.future);
  }

  Widget _categoriesPanel(
    BuildContext context,
    List<CategoryDto> categories, {
    required String? selectedCategoryId,
    required bool isEn,
  }) {
    final strings = ref.read(stringsProvider);
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: 8),
      children: [
        _categoryRow(
          context,
          label: strings.tr('Tous', 'All'),
          selected: selectedCategoryId == null,
          onTap: () {
            ref.read(categoriesSelectedCategoryIdProvider.notifier).state =
                null;
          },
        ),
        for (final category in categories)
          _categoryRow(
            context,
            label: isEn &&
                    category.nameEn != null &&
                    category.nameEn!.trim().isNotEmpty
                ? category.nameEn!
                : category.name,
            selected: selectedCategoryId == category.id,
            onTap: () {
              ref.read(categoriesSelectedCategoryIdProvider.notifier).state =
                  category.id;
            },
          ),
      ],
    );
  }

  Widget _categoryRow(
    BuildContext context, {
    required String label,
    required bool selected,
    required VoidCallback onTap,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return InkWell(
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.fromLTRB(6, 0, 6, 6),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 9),
        decoration: BoxDecoration(
          color: selected
              ? DmColors.doorBlue
              : (isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight),
          borderRadius: DmRadius.r12,
          border: Border.all(color: DmColors.border(isDark)),
        ),
        child: Text(
          label,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: selected
                    ? Colors.white
                    : (DmColors.iconFg(isDark)),
                fontWeight: FontWeight.w600,
              ),
        ),
      ),
    );
  }

  Widget _productsGrid(BuildContext context, List<ProductDto> products) {
    final strings = ref.read(stringsProvider);
    if (products.isEmpty) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          const SizedBox(height: 22),
          Center(
            child: Text(
              strings.tr('Aucun produit', 'No products'),
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        ],
      );
    }

    return GridView.builder(
      padding: const EdgeInsets.only(bottom: 12),
      physics: const AlwaysScrollableScrollPhysics(),
      cacheExtent: 900,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        crossAxisSpacing: 3,
        mainAxisSpacing: 3,
        childAspectRatio: 0.62,
      ),
      itemCount: products.length,
      itemBuilder: (context, index) => _productCard(context, products[index]),
    );
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
              child: Stack(
                children: [
                  DmNetworkImage(
                    url: product.mainImageUrl,
                    width: double.infinity,
                    fit: BoxFit.cover,
                    borderRadius: DmRadius.r12,
                    cacheWidth: 260,
                    cacheHeight: 340,
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
              style: Theme.of(context).textTheme.bodySmall,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            Text(
              '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: DmColors.doorOrange,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            if ((product.shopReviewCount ?? 0) > 0)
              Text(
                '${product.shopRating?.toStringAsFixed(1) ?? '-'} (${product.shopReviewCount})',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      fontSize: 10,
                      color: DmColors.mutedText(isDark),
                    ),
              ),
          ],
        ),
      ),
    );
  }
}

