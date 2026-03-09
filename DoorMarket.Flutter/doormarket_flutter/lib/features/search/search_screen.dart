import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/models/categories.dart';
import '../../core/models/common.dart';
import '../../core/models/products.dart';
import '../../core/models/search.dart';
import '../../core/models/shops.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';

class SearchScreen extends ConsumerStatefulWidget {
  final String? initialQuery;

  const SearchScreen({super.key, this.initialQuery});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

enum _SearchTab { products, shops, categories }

class _SearchScreenState extends ConsumerState<SearchScreen> {
  static const String _analyticsSessionStorageKey =
      'dm_search_analytics_session_id';
  final _queryController = TextEditingController();
  Timer? _suggestionDebounce;
  int _searchRequestVersion = 0;

  _SearchTab _tab = _SearchTab.products;
  String? _query;

  bool _loading = false;
  String? _error;
  bool _loadingSuggestions = false;
  List<SearchSuggestionDto> _suggestions = const <SearchSuggestionDto>[];

  PagedResult<ProductDto>? _products;
  PagedResult<ShopDto>? _shops;
  List<SearchCategoryDto> _categories = const <SearchCategoryDto>[];
  List<CategoryDto> _allCategories = const <CategoryDto>[];
  List<ShopDto> _shopLookup = const <ShopDto>[];

  int _productPage = 1;
  int _shopPage = 1;

  bool _inStockOnly = false;
  bool _promotedOnly = false;
  String _productSort = 'relevance';
  double? _productMinPrice;
  double? _productMaxPrice;
  double? _productRatingMin;
  String? _productShopId;
  String? _categoryId;
  String? _productCity;
  String? _productCountryTag;

  bool _verifiedOnly = false;
  bool _recommendedOnly = false;
  String _shopSort = 'relevance';
  double? _shopRatingMin;
  String? _shopCategoryId;
  String? _shopCity;
  String? _countryTag;
  String? _analyticsSessionId;
  bool _analyticsSessionReady = false;

  @override
  void initState() {
    super.initState();
    final initial = (widget.initialQuery ?? '').trim();
    if (initial.isNotEmpty) {
      _query = initial;
      _queryController.text = initial;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _initializeAnalyticsSession();
      _loadLookup();
      _search(resetPage: false);
    });
  }

  void _onQueryChanged(String value) {
    final normalized = value.trim();
    _query = normalized.isEmpty ? null : normalized;
    _suggestionDebounce?.cancel();

    if (_query == null || _query!.length < 2) {
      if (mounted) {
        setState(() {
          _suggestions = const <SearchSuggestionDto>[];
          _loadingSuggestions = false;
        });
      }
      if ((_query ?? '').isEmpty) {
        _search();
      }
      return;
    }

    setState(() => _loadingSuggestions = true);
    _suggestionDebounce = Timer(const Duration(milliseconds: 180), () async {
      final currentQuery = _query;
      if (currentQuery == null || currentQuery.length < 2) {
        if (!mounted) return;
        setState(() {
          _suggestions = const <SearchSuggestionDto>[];
          _loadingSuggestions = false;
        });
        return;
      }

      final api = ref.read(searchApiProvider);
      try {
        final rows = await api.suggestions(currentQuery, limit: 8);
        if (!mounted) return;
        setState(() {
          _suggestions = rows;
          _loadingSuggestions = false;
        });
        _search();
      } catch (_) {
        if (!mounted) return;
        setState(() {
          _suggestions = const <SearchSuggestionDto>[];
          _loadingSuggestions = false;
        });
      }
    });
  }

  void _applySuggestion(SearchSuggestionDto suggestion, int position) {
    _queryController.text = suggestion.value;
    setState(() {
      _query = suggestion.value.trim().isEmpty ? null : suggestion.value.trim();
      _suggestions = const <SearchSuggestionDto>[];
    });

    if ((suggestion.entityId ?? '').isNotEmpty) {
      if (suggestion.type.toLowerCase() == 'product') {
        _trackClickEvent(
            targetType: 'Product',
            targetId: suggestion.entityId!,
            position: position);
      } else if (suggestion.type.toLowerCase() == 'shop') {
        _trackClickEvent(
            targetType: 'Shop',
            targetId: suggestion.entityId!,
            position: position);
      } else if (suggestion.type.toLowerCase() == 'category') {
        _trackClickEvent(
            targetType: 'Category',
            targetId: suggestion.entityId!,
            position: position);
      }
    }

    _search();
  }

  void _submitSearch([String? raw]) {
    final normalized = (raw ?? _queryController.text).trim();
    setState(() {
      _query = normalized.isEmpty ? null : normalized;
      _suggestions = const <SearchSuggestionDto>[];
      _loadingSuggestions = false;
    });
    _search();
  }

  String _suggestionTypeLabel(dynamic strings, String type) {
    switch (type.toLowerCase()) {
      case 'product':
        return strings.tr('Produit', 'Product');
      case 'shop':
        return strings.tr('Boutique', 'Shop');
      case 'category':
        return strings.tr('Categorie', 'Category');
      case 'query':
        return strings.tr('Populaire', 'Popular');
      default:
        return type;
    }
  }

  String _filtersSummary(dynamic strings) {
    if (_tab == _SearchTab.products) {
      final parts = <String>[
        '${strings.tr('Tri', 'Sort')}: $_productSort',
        '${strings.tr('Stock', 'Stock')}: ${_inStockOnly ? 'on' : 'off'}',
        '${strings.tr('Promo', 'Promo')}: ${_promotedOnly ? 'on' : 'off'}',
      ];
      if (_productMinPrice != null) {
        parts.add('min=${_productMinPrice!.toStringAsFixed(0)}');
      }
      if (_productMaxPrice != null) {
        parts.add('max=${_productMaxPrice!.toStringAsFixed(0)}');
      }
      if (_productRatingMin != null) {
        parts.add('rating>=${_productRatingMin!.toStringAsFixed(1)}');
      }
      return parts.join(' | ');
    }

    if (_tab == _SearchTab.shops) {
      final parts = <String>[
        '${strings.tr('Tri', 'Sort')}: $_shopSort',
        '${strings.tr('Verifiees', 'Verified')}: ${_verifiedOnly ? 'on' : 'off'}',
      ];
      if (_shopRatingMin != null) {
        parts.add('rating>=${_shopRatingMin!.toStringAsFixed(1)}');
      }
      if ((_countryTag ?? '').isNotEmpty) {
        parts.add('country=${_countryTag!}');
      }
      if ((_shopCity ?? '').isNotEmpty) {
        parts.add('city=${_shopCity!}');
      }
      return parts.join(' | ');
    }

    return strings.tr('Filtres categories', 'Category filters');
  }

  Future<void> _openFiltersSheet(BuildContext context, dynamic strings) async {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) {
        return SafeArea(
          child: Padding(
            padding: EdgeInsets.only(
              left: 16,
              right: 16,
              top: 8,
              bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
            ),
            child: SingleChildScrollView(
              child: _buildFilters(context, strings, isDark),
            ),
          ),
        );
      },
    );
  }

  @override
  void dispose() {
    _suggestionDebounce?.cancel();
    _queryController.dispose();
    super.dispose();
  }

  Future<void> _loadLookup() async {
    try {
      final api = ref.read(categoriesApiProvider);
      final items = await api.getAll();
      final shopsPage = await ref.read(searchApiProvider).shops(
            const SearchShopsQuery(
              page: 1,
              pageSize: 120,
              verifiedOnly: false,
              recommended: false,
              sort: 'popular_desc',
            ),
          );
      if (!mounted) return;
      setState(() {
        _allCategories = items;
        _shopLookup = shopsPage.items;
      });
    } catch (_) {}
  }

  Future<void> _search({bool resetPage = true}) async {
    if (resetPage) {
      _productPage = 1;
      _shopPage = 1;
    }

    final requestVersion = ++_searchRequestVersion;
    setState(() {
      _loading = true;
      _error = null;
    });

    final api = ref.read(searchApiProvider);
    final stopwatch = Stopwatch()..start();
    var resultsCount = 0;

    try {
      if (_tab == _SearchTab.products) {
        final result = await api.products(
          SearchProductsQuery(
            q: _query,
            page: _productPage,
            pageSize: 12,
            inStockOnly: _inStockOnly,
            promotedOnly: _promotedOnly,
            minPrice: _productMinPrice,
            maxPrice: _productMaxPrice,
            ratingMin: _productRatingMin,
            shopId: _productShopId,
            categoryId: _categoryId,
            city: _productCity,
            countryTag: _productCountryTag,
            sort: _productSort,
          ),
        );

        if (!mounted || requestVersion != _searchRequestVersion) return;
        setState(() => _products = result);
        resultsCount = result.total;
      } else if (_tab == _SearchTab.shops) {
        final result = await api.shops(
          SearchShopsQuery(
            q: _query,
            page: _shopPage,
            pageSize: 12,
            verifiedOnly: _verifiedOnly,
            recommended: _recommendedOnly,
            ratingMin: _shopRatingMin,
            categoryId: _shopCategoryId,
            city: _shopCity,
            countryTag: _countryTag,
            sort: _shopSort,
          ),
        );

        if (!mounted || requestVersion != _searchRequestVersion) return;
        setState(() => _shops = result);
        resultsCount = result.total;
      } else {
        final result = await api.categories(
          q: _query,
          countryTag: _countryTag,
          limit: 24,
        );

        if (!mounted || requestVersion != _searchRequestVersion) return;
        setState(() => _categories = result);
        resultsCount = result.length;
      }

      stopwatch.stop();
      _trackQueryEvent(
        resultsCount: resultsCount,
        durationMs: stopwatch.elapsedMilliseconds,
      );
    } catch (e) {
      if (!mounted || requestVersion != _searchRequestVersion) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted && requestVersion == _searchRequestVersion) {
        setState(() => _loading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final strings = ref.watch(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: strings.tr('Recherche', 'Search'),
            subtitle: strings.tr(
              'Trouvez des produits, boutiques et categories.',
              'Find products, shops, and categories.',
            ),
            showBack: true,
            onBack: () => context.pop(),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(DmSpacing.xxl),
              children: [
                DmSearchBar(
                  hint: strings.tr(
                      'Que cherchez-vous ?', 'What are you looking for?'),
                  controller: _queryController,
                  onChanged: _onQueryChanged,
                  onSubmitted: _submitSearch,
                  trailingIcon: Icons.arrow_forward,
                  onTrailingTap: _submitSearch,
                ),
                if (_loadingSuggestions)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: LinearProgressIndicator(minHeight: 2),
                  ),
                if (_suggestions.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: DmCard(
                      child: Column(
                        children:
                            _suggestions.take(6).toList().asMap().entries.map(
                          (entry) {
                            final index = entry.key;
                            final s = entry.value;
                            return ListTile(
                              key: ValueKey('${s.type}-${s.value}-$index'),
                              dense: true,
                              contentPadding: EdgeInsets.zero,
                              title: Text(s.label,
                                  maxLines: 1, overflow: TextOverflow.ellipsis),
                              subtitle: s.subtitle == null
                                  ? null
                                  : Text(
                                      s.subtitle!,
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                              trailing: Text(
                                _suggestionTypeLabel(strings, s.type),
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                              onTap: () {
                                _applySuggestion(s, index + 1);
                              },
                            );
                          },
                        ).toList(),
                      ),
                    ),
                  ),
                const SizedBox(height: 14),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _tabChip(strings.tr('Produits', 'Products'),
                        _SearchTab.products),
                    _tabChip(
                        strings.tr('Boutiques', 'Shops'), _SearchTab.shops),
                    _tabChip(strings.tr('Categories', 'Categories'),
                        _SearchTab.categories),
                  ],
                ),
                const SizedBox(height: 14),
                DmCard(
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _filtersSummary(strings),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: 10),
                      ElevatedButton.icon(
                        onPressed: () => _openFiltersSheet(context, strings),
                        icon: const Icon(Icons.tune, size: 18),
                        label: Text(strings.tr('Filtres', 'Filters')),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                if (_loading)
                  const Center(
                      child: Padding(
                    padding: EdgeInsets.all(24),
                    child: CircularProgressIndicator(),
                  ))
                else if (_error != null)
                  Text(strings.tr('Erreur: $_error', 'Error: $_error'))
                else
                  _buildResults(context, strings, isDark),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFilters(BuildContext context, dynamic strings, bool isDark) {
    if (_tab == _SearchTab.products) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          DropdownButtonFormField<String>(
            initialValue: _productSort,
            items: const [
              DropdownMenuItem(value: 'relevance', child: Text('Relevance')),
              DropdownMenuItem(
                  value: 'price_asc', child: Text('Price low-high')),
              DropdownMenuItem(
                  value: 'price_desc', child: Text('Price high-low')),
              DropdownMenuItem(value: 'rating_desc', child: Text('Top rated')),
              DropdownMenuItem(value: 'newest', child: Text('Newest')),
            ],
            onChanged: (v) => setState(() => _productSort = v ?? 'relevance'),
            decoration: InputDecoration(labelText: strings.tr('Tri', 'Sort')),
          ),
          const SizedBox(height: 10),
          DropdownButtonFormField<String?>(
            initialValue: _categoryId,
            items: [
              DropdownMenuItem<String?>(
                  value: null,
                  child:
                      Text(strings.tr('Toutes categories', 'All categories'))),
              ..._allCategories.map(
                (c) => DropdownMenuItem<String?>(
                  value: c.id,
                  child: Text(_catLabel(context, c)),
                ),
              ),
            ],
            onChanged: (v) => setState(() => _categoryId = v),
            decoration:
                InputDecoration(labelText: strings.tr('Categorie', 'Category')),
          ),
          const SizedBox(height: 10),
          DropdownButtonFormField<String?>(
            initialValue: _productShopId,
            items: [
              DropdownMenuItem<String?>(
                value: null,
                child: Text(strings.tr('Toutes boutiques', 'All shops')),
              ),
              ..._shopLookup.map(
                (s) => DropdownMenuItem<String?>(
                  value: s.id,
                  child: Text(s.name),
                ),
              ),
            ],
            onChanged: (v) => setState(() => _productShopId = v),
            decoration:
                InputDecoration(labelText: strings.tr('Boutique', 'Shop')),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextFormField(
                  initialValue: _productMinPrice?.toString(),
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  decoration: InputDecoration(
                      labelText: strings.tr('Prix min', 'Min price')),
                  onChanged: (v) =>
                      _productMinPrice = double.tryParse(v.trim()),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: TextFormField(
                  initialValue: _productMaxPrice?.toString(),
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  decoration: InputDecoration(
                      labelText: strings.tr('Prix max', 'Max price')),
                  onChanged: (v) =>
                      _productMaxPrice = double.tryParse(v.trim()),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _productRatingMin?.toString(),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(
                labelText: strings.tr('Note min', 'Min rating')),
            onChanged: (v) => _productRatingMin = double.tryParse(v.trim()),
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _productCity,
            decoration: InputDecoration(labelText: strings.tr('Ville', 'City')),
            onChanged: (v) => _productCity = v.trim().isEmpty ? null : v.trim(),
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _productCountryTag,
            decoration: InputDecoration(
                labelText: strings.tr('Pays (code)', 'Country code')),
            onChanged: (v) =>
                _productCountryTag = v.trim().isEmpty ? null : v.trim(),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: SwitchListTile.adaptive(
                  value: _inStockOnly,
                  onChanged: (v) => setState(() => _inStockOnly = v),
                  title: Text(strings.tr('En stock', 'In stock')),
                  contentPadding: EdgeInsets.zero,
                ),
              ),
              Expanded(
                child: SwitchListTile.adaptive(
                  value: _promotedOnly,
                  onChanged: (v) => setState(() => _promotedOnly = v),
                  title: Text(strings.tr('Promos', 'Promoted')),
                  contentPadding: EdgeInsets.zero,
                ),
              ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: ElevatedButton(
              onPressed: () {
                _productPage = 1;
                Navigator.of(context).pop();
                _search();
              },
              child: Text(strings.tr('Appliquer', 'Apply')),
            ),
          ),
        ],
      );
    }

    if (_tab == _SearchTab.shops) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          DropdownButtonFormField<String>(
            initialValue: _shopSort,
            items: const [
              DropdownMenuItem(value: 'relevance', child: Text('Relevance')),
              DropdownMenuItem(value: 'rating_desc', child: Text('Top rated')),
              DropdownMenuItem(value: 'popular_desc', child: Text('Popular')),
              DropdownMenuItem(value: 'newest', child: Text('Newest')),
            ],
            onChanged: (v) => setState(() => _shopSort = v ?? 'relevance'),
            decoration: InputDecoration(labelText: strings.tr('Tri', 'Sort')),
          ),
          const SizedBox(height: 10),
          DropdownButtonFormField<String?>(
            initialValue: _shopCategoryId,
            items: [
              DropdownMenuItem<String?>(
                value: null,
                child: Text(strings.tr('Toutes categories', 'All categories')),
              ),
              ..._allCategories.map(
                (c) => DropdownMenuItem<String?>(
                  value: c.id,
                  child: Text(_catLabel(context, c)),
                ),
              ),
            ],
            onChanged: (v) => setState(() => _shopCategoryId = v),
            decoration:
                InputDecoration(labelText: strings.tr('Categorie', 'Category')),
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _shopRatingMin?.toString(),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(
                labelText: strings.tr('Note min', 'Min rating')),
            onChanged: (v) => _shopRatingMin = double.tryParse(v.trim()),
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _shopCity,
            decoration: InputDecoration(
                labelText: strings.tr('Ville (optionnel)', 'City (optional)')),
            onChanged: (v) => _shopCity = v.trim().isEmpty ? null : v.trim(),
          ),
          const SizedBox(height: 10),
          TextFormField(
            initialValue: _countryTag,
            decoration: InputDecoration(
                labelText:
                    strings.tr('Pays (optionnel)', 'Country (optional)')),
            onChanged: (v) => _countryTag = v.trim().isEmpty ? null : v.trim(),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: SwitchListTile.adaptive(
                  value: _verifiedOnly,
                  onChanged: (v) => setState(() => _verifiedOnly = v),
                  title: Text(strings.tr('Verifiees', 'Verified')),
                  contentPadding: EdgeInsets.zero,
                ),
              ),
              Expanded(
                child: SwitchListTile.adaptive(
                  value: _recommendedOnly,
                  onChanged: (v) => setState(() => _recommendedOnly = v),
                  title: Text(strings.tr('Recommandees', 'Recommended')),
                  contentPadding: EdgeInsets.zero,
                ),
              ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: ElevatedButton(
              onPressed: () {
                _shopPage = 1;
                Navigator.of(context).pop();
                _search();
              },
              child: Text(strings.tr('Appliquer', 'Apply')),
            ),
          ),
        ],
      );
    }

    return Align(
      alignment: Alignment.centerRight,
      child: ElevatedButton(
        onPressed: () {
          Navigator.of(context).pop();
          _search(resetPage: false);
        },
        child: Text(strings.tr('Rafraichir', 'Refresh')),
      ),
    );
  }

  Widget _buildResults(BuildContext context, dynamic strings, bool isDark) {
    if (_tab == _SearchTab.products) {
      final items = _products?.items ?? const <ProductDto>[];
      if (items.isEmpty) {
        return Text(strings.tr('Aucun produit', 'No products'));
      }
      return Column(
        children: [
          ...items.asMap().entries.map((entry) {
            final index = entry.key;
            final p = entry.value;
            return Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: DmCard(
                boxShadowOverride: const [],
                child: Row(
                  children: [
                    DmNetworkImage(
                        url: p.mainImageUrl,
                        width: 72,
                        height: 72,
                        borderRadius: DmRadius.r12,
                        cacheWidth: 220,
                        cacheHeight: 220),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(p.name,
                              maxLines: 1, overflow: TextOverflow.ellipsis),
                          Text(p.shopName,
                              style: Theme.of(context).textTheme.bodySmall),
                          Text(
                              '${p.currency} ${p.effectivePrice.toStringAsFixed(2)}',
                              style: const TextStyle(
                                  color: DmColors.doorOrange,
                                  fontWeight: FontWeight.w700)),
                        ],
                      ),
                    ),
                    IconButton(
                      onPressed: () {
                        _trackClickEvent(
                          targetType: 'Product',
                          targetId: p.id,
                          position: index + 1,
                        );
                        context.push('/product/${p.id}');
                      },
                      icon: const Icon(Icons.arrow_forward_ios, size: 16),
                    ),
                  ],
                ),
              ),
            );
          }),
          _pager(
            page: _products?.page ?? 1,
            total: _products?.total ?? 0,
            pageSize: _products?.pageSize ?? 12,
            onPrev: () {
              if ((_products?.page ?? 1) > 1) {
                setState(() => _productPage -= 1);
                _search(resetPage: false);
              }
            },
            onNext: () {
              final p = _products?.page ?? 1;
              final totalPages =
                  ((_products?.total ?? 0) / (_products?.pageSize ?? 12))
                      .ceil();
              if (p < totalPages) {
                setState(() => _productPage += 1);
                _search(resetPage: false);
              }
            },
          ),
        ],
      );
    }

    if (_tab == _SearchTab.shops) {
      final items = _shops?.items ?? const <ShopDto>[];
      if (items.isEmpty) return Text(strings.tr('Aucune boutique', 'No shops'));
      return Column(
        children: [
          ...items.asMap().entries.map((entry) {
            final index = entry.key;
            final s = entry.value;
            return Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: DmCard(
                boxShadowOverride: const [],
                child: Row(
                  children: [
                    DmNetworkImage(
                        url: s.imageUrl,
                        width: 72,
                        height: 72,
                        borderRadius: DmRadius.r12,
                        cacheWidth: 220,
                        cacheHeight: 220),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(s.name,
                              maxLines: 1, overflow: TextOverflow.ellipsis),
                          Text('${s.city}, ${s.countryTag}',
                              style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ),
                    ),
                    IconButton(
                      onPressed: () {
                        _trackClickEvent(
                          targetType: 'Shop',
                          targetId: s.id,
                          position: index + 1,
                        );
                        context.push('/shops/${s.id}/products');
                      },
                      icon: const Icon(Icons.storefront, size: 18),
                    ),
                  ],
                ),
              ),
            );
          }),
          _pager(
            page: _shops?.page ?? 1,
            total: _shops?.total ?? 0,
            pageSize: _shops?.pageSize ?? 12,
            onPrev: () {
              if ((_shops?.page ?? 1) > 1) {
                setState(() => _shopPage -= 1);
                _search(resetPage: false);
              }
            },
            onNext: () {
              final p = _shops?.page ?? 1;
              final totalPages =
                  ((_shops?.total ?? 0) / (_shops?.pageSize ?? 12)).ceil();
              if (p < totalPages) {
                setState(() => _shopPage += 1);
                _search(resetPage: false);
              }
            },
          ),
        ],
      );
    }

    if (_categories.isEmpty) {
      return Text(strings.tr('Aucune categorie', 'No categories'));
    }

    return Column(
      children: _categories.asMap().entries.map((entry) {
        final index = entry.key;
        final c = entry.value;
        return Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: DmCard(
            boxShadowOverride: const [],
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(_catName(c),
                    style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  children: [
                    _metricChip(
                        '${c.activeProductsCount} ${strings.tr('produits', 'products')}',
                        isDark),
                    _metricChip(
                        '${c.activeShopsCount} ${strings.tr('boutiques', 'shops')}',
                        isDark),
                  ],
                ),
                const SizedBox(height: 8),
                TextButton(
                  onPressed: () {
                    _trackClickEvent(
                      targetType: 'Category',
                      targetId: c.id,
                      position: index + 1,
                    );
                    context.push('/categories/${c.id}');
                  },
                  child: Text(strings.tr('Voir produits', 'View products')),
                ),
              ],
            ),
          ),
        );
      }).toList(),
    );
  }

  Future<void> _initializeAnalyticsSession() async {
    if (_analyticsSessionReady && _analyticsSessionId != null) {
      return;
    }

    if (_analyticsSessionReady) {
      _analyticsSessionId ??= _buildSessionId();
      return;
    }

    _analyticsSessionReady = true;
    try {
      final prefs = await SharedPreferences.getInstance();
      final existing = prefs.getString(_analyticsSessionStorageKey);
      if (existing != null && existing.trim().isNotEmpty) {
        _analyticsSessionId = existing.trim();
        return;
      }

      final generated = _buildSessionId();
      await prefs.setString(_analyticsSessionStorageKey, generated);
      _analyticsSessionId = generated;
    } catch (_) {
      _analyticsSessionId ??= _buildSessionId();
    }
  }

  void _trackQueryEvent({
    required int resultsCount,
    required int durationMs,
  }) {
    final query = _query?.trim();
    if (query == null || query.isEmpty) {
      return;
    }

    unawaited(_trackQueryEventAsync(
      query: query,
      resultsCount: resultsCount,
      durationMs: durationMs,
    ));
  }

  Future<void> _trackQueryEventAsync({
    required String query,
    required int resultsCount,
    required int durationMs,
  }) async {
    final api = ref.read(searchApiProvider);
    await _initializeAnalyticsSession();

    try {
      await api.trackQuery(
        SearchAnalyticsQueryEvent(
          query: _normalizeQuery(query),
          resultsCount: resultsCount < 0 ? 0 : resultsCount,
          durationMs: durationMs < 0 ? 0 : durationMs,
          page: _tab == _SearchTab.products
              ? _productPage
              : (_tab == _SearchTab.shops ? _shopPage : 1),
          sort: _tab == _SearchTab.products
              ? _productSort
              : (_tab == _SearchTab.shops ? _shopSort : 'relevance'),
          filtersHash: _buildFiltersSignature(),
          sessionId: _analyticsSessionId,
          source: 'Mobile',
          countryTag: _normalizeCountryTag(_countryTag),
        ),
      );
    } catch (_) {}
  }

  void _trackClickEvent({
    required String targetType,
    required String targetId,
    required int position,
  }) {
    final query = _query?.trim();
    unawaited(_trackClickEventAsync(
      query: query,
      targetType: targetType,
      targetId: targetId,
      position: position,
    ));
  }

  Future<void> _trackClickEventAsync({
    required String? query,
    required String targetType,
    required String targetId,
    required int position,
  }) async {
    if (targetId.trim().isEmpty) {
      return;
    }

    final api = ref.read(searchApiProvider);
    await _initializeAnalyticsSession();

    try {
      await api.trackClick(
        SearchAnalyticsClickEvent(
          query: _normalizeQuery(query),
          targetType: targetType,
          targetId: targetId,
          position: position <= 0 ? 1 : position,
          page: _tab == _SearchTab.products
              ? _productPage
              : (_tab == _SearchTab.shops ? _shopPage : 1),
          sort: _tab == _SearchTab.products
              ? _productSort
              : (_tab == _SearchTab.shops ? _shopSort : 'relevance'),
          filtersHash: _buildFiltersSignature(),
          sessionId: _analyticsSessionId,
          source: 'Mobile',
          countryTag: _normalizeCountryTag(_countryTag),
        ),
      );
    } catch (_) {}
  }

  String _buildFiltersSignature() {
    if (_tab == _SearchTab.products) {
      return 'inStock=${_inStockOnly ? 1 : 0}|promo=${_promotedOnly ? 1 : 0}|category=${_categoryId ?? '-'}';
    }

    if (_tab == _SearchTab.shops) {
      return 'verified=${_verifiedOnly ? 1 : 0}|recommended=${_recommendedOnly ? 1 : 0}|country=${_normalizeCountryTag(_countryTag) ?? '-'}';
    }

    return 'limit=24|country=${_normalizeCountryTag(_countryTag) ?? '-'}';
  }

  String _buildSessionId() {
    final now = DateTime.now().microsecondsSinceEpoch.toRadixString(16);
    final rnd = Random().nextInt(1 << 31).toRadixString(16);
    return 'm$now$rnd';
  }

  String? _normalizeQuery(String? value) {
    final normalized = (value ?? '').trim();
    if (normalized.isEmpty) {
      return null;
    }

    if (normalized.length <= 80) {
      return normalized;
    }
    return normalized.substring(0, 80);
  }

  String? _normalizeCountryTag(String? value) {
    final normalized = (value ?? '').trim();
    if (normalized.isEmpty) {
      return null;
    }

    final upper = normalized.toUpperCase();
    if (upper.length <= 8) {
      return upper;
    }
    return upper.substring(0, 8);
  }

  Widget _metricChip(String label, bool isDark) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight,
        borderRadius: DmRadius.r16,
        border: Border.all(color: DmColors.border(isDark)),
      ),
      child: Text(label,
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }

  Widget _tabChip(String label, _SearchTab tab) {
    final selected = _tab == tab;
    return ChoiceChip(
      selected: selected,
      label: Text(label),
      onSelected: (_) {
        setState(() => _tab = tab);
        _search(resetPage: false);
      },
    );
  }

  Widget _pager({
    required int page,
    required int total,
    required int pageSize,
    required VoidCallback onPrev,
    required VoidCallback onNext,
  }) {
    final totalPages =
        pageSize <= 0 ? 1 : (total / pageSize).ceil().clamp(1, 9999);
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text('Page $page / $totalPages'),
        Row(
          children: [
            IconButton(onPressed: onPrev, icon: const Icon(Icons.chevron_left)),
            IconButton(
                onPressed: onNext, icon: const Icon(Icons.chevron_right)),
          ],
        ),
      ],
    );
  }

  String _catLabel(BuildContext context, CategoryDto c) {
    final isEn =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'en';
    if (isEn && c.nameEn != null && c.nameEn!.isNotEmpty) {
      return c.nameEn!;
    }
    return c.name;
  }

  String _catName(SearchCategoryDto c) {
    final isEn =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'en';
    if (isEn && c.nameEn != null && c.nameEn!.isNotEmpty) {
      return c.nameEn!;
    }
    return c.name;
  }
}
