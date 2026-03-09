import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/shops.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_fade_slide.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_search_bar.dart';
import 'shops_provider.dart';

class ShopsScreen extends ConsumerStatefulWidget {
  const ShopsScreen({super.key});

  @override
  ConsumerState<ShopsScreen> createState() => _ShopsScreenState();
}

class _ShopsScreenState extends ConsumerState<ShopsScreen>
    with AutomaticKeepAliveClientMixin {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final data = ref.watch(shopsDataProvider);
    final strings = ref.watch(stringsProvider);
    final locale = Localizations.localeOf(context);
    final isEn = locale.languageCode.toLowerCase() == 'en';

    return Scaffold(
      body: data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) =>
            Center(child: Text(strings.tr('Erreur: $err', 'Error: $err'))),
        data: (value) => Column(
          children: [
            _header(context, value.shops.length),
            Expanded(
              child: CustomScrollView(
                physics: const BouncingScrollPhysics(),
                slivers: [
                  SliverPadding(
                    padding:
                        const EdgeInsets.symmetric(horizontal: DmSpacing.xxl),
                    sliver: SliverToBoxAdapter(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          DmSearchBar(
                            hint: strings.tr(
                                'Rechercher une boutique', 'Search a shop'),
                            controller: _searchController,
                            onSubmitted: (value) {
                              ref.read(shopsSearchProvider.notifier).state =
                                  value.trim().isEmpty ? null : value.trim();
                              ref.invalidate(shopsDataProvider);
                            },
                          ),
                          const SizedBox(height: 16),
                          DmFadeSlide(
                            delay: const Duration(milliseconds: 80),
                            child: DmCard(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(strings.tr('Filtres', 'Filters'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium),
                                  const SizedBox(height: 12),
                                  Wrap(
                                    spacing: 12,
                                    runSpacing: 12,
                                    children: [
                                      SizedBox(
                                        width: 200,
                                        child: _countryDropdown(
                                            context, value.shops),
                                      ),
                                      Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          Text(
                                              strings.tr(
                                                  'Recommande', 'Recommended'),
                                              style: const TextStyle(
                                                  fontSize: 12)),
                                          const SizedBox(width: 6),
                                          Switch.adaptive(
                                            value: ref.watch(
                                                shopsRecommendedProvider),
                                            onChanged: (val) {
                                              ref
                                                  .read(shopsRecommendedProvider
                                                      .notifier)
                                                  .state = val;
                                              ref.invalidate(shopsDataProvider);
                                            },
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(height: 16),
                          Text(strings.tr('Categories', 'Categories'),
                              style: Theme.of(context).textTheme.bodyMedium),
                          const SizedBox(height: 8),
                          SizedBox(
                            height: 46,
                            child: ListView.separated(
                              scrollDirection: Axis.horizontal,
                              itemCount: value.categories.length + 1,
                              separatorBuilder: (_, __) =>
                                  const SizedBox(width: 8),
                              itemBuilder: (context, index) {
                                if (index == 0) {
                                  return _categoryChip(
                                    context,
                                    strings.tr('Tous', 'All'),
                                    isSelected:
                                        ref.watch(shopsCategoryProvider) ==
                                            null,
                                    onTap: () {
                                      ref
                                          .read(shopsCategoryProvider.notifier)
                                          .state = null;
                                      ref.invalidate(shopsDataProvider);
                                    },
                                  );
                                }
                                final category = value.categories[index - 1];
                                final displayName = isEn &&
                                        category.nameEn != null &&
                                        category.nameEn!.isNotEmpty
                                    ? category.nameEn!
                                    : category.name;
                                return _categoryChip(
                                  context,
                                  displayName,
                                  isSelected:
                                      ref.watch(shopsCategoryProvider) ==
                                          category.id,
                                  onTap: () {
                                    ref
                                        .read(shopsCategoryProvider.notifier)
                                        .state = category.id;
                                    ref.invalidate(shopsDataProvider);
                                  },
                                );
                              },
                            ),
                          ),
                          const SizedBox(height: 16),
                          if (value.shops.isEmpty)
                            Text(
                              strings.tr('Aucune boutique disponible',
                                  'No shop available'),
                              style: TextStyle(
                                color: DmColors.mutedText(
                                  Theme.of(context).brightness ==
                                      Brightness.dark,
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ),
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(
                        DmSpacing.xxl, 0, DmSpacing.xxl, DmSpacing.xxxl),
                    sliver: SliverList(
                      delegate: SliverChildBuilderDelegate(
                        (context, index) =>
                            _shopCard(context, value.shops[index]),
                        childCount: value.shops.length,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  bool get wantKeepAlive => true;

  Widget _header(BuildContext context, int count) {
    final strings = ref.read(stringsProvider);
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [DmColors.doorBlue, DmColors.doorBlue.withAlpha(235)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(28),
          bottomRight: Radius.circular(28),
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 18, 24, 22),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    height: 42,
                    width: 42,
                    decoration: BoxDecoration(
                      color: Colors.white.withAlpha(24),
                      borderRadius: DmRadius.r12,
                    ),
                    child: const Icon(Icons.storefront, color: Colors.white),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      strings.tr('Boutiques', 'Shops'),
                      style: const TextStyle(
                          color: Colors.white,
                          fontSize: 20,
                          fontWeight: FontWeight.w700),
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.white.withAlpha(28),
                      borderRadius: DmRadius.r16,
                    ),
                    child: Text(
                      strings.tr('$count boutiques', '$count shops'),
                      style: const TextStyle(color: Colors.white, fontSize: 11),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                strings.tr(
                  'Trouvez des vendeurs locaux et des boutiques certifiees.',
                  'Find local sellers and certified shops.',
                ),
                style: const TextStyle(color: Color(0xFFD9E5F8), fontSize: 12),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _countryDropdown(BuildContext context, List<ShopDto> shops) {
    final strings = ref.read(stringsProvider);
    final countries = <String>{};
    for (final shop in shops) {
      if (shop.countryTag.isNotEmpty) countries.add(shop.countryTag);
    }
    final items = [strings.tr('Tous', 'All'), ...countries];
    final selected = ref.watch(shopsCountryProvider);

    return DropdownButtonFormField<String>(
      key: ValueKey(selected == null || selected.isEmpty
          ? strings.tr('Tous', 'All')
          : selected),
      initialValue: selected == null || selected.isEmpty
          ? strings.tr('Tous', 'All')
          : selected,
      items:
          items.map((c) => DropdownMenuItem(value: c, child: Text(c))).toList(),
      onChanged: (value) {
        final newValue = (value == null || value == strings.tr('Tous', 'All'))
            ? null
            : value;
        ref.read(shopsCountryProvider.notifier).state = newValue;
        ref.invalidate(shopsDataProvider);
      },
      decoration: InputDecoration(labelText: strings.tr('Pays', 'Country')),
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
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
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
          ),
        ),
      ),
    );
  }

  Widget _shopCard(BuildContext context, ShopDto shop) {
    final strings = ref.read(stringsProvider);
    return DmFadeSlide(
      delay: const Duration(milliseconds: 120),
      beginOffset: const Offset(0, 0.04),
      child: DmCard(
        margin: const EdgeInsets.only(bottom: 10),
        child: Row(
          children: [
            DmNetworkImage(
              url: shop.imageUrl,
              height: 72,
              width: 72,
              borderRadius: DmRadius.r14,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(shop.name,
                      style: Theme.of(context).textTheme.bodyMedium,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 4),
                  Text(shop.city, style: Theme.of(context).textTheme.bodySmall),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      if (shop.countryTag.isNotEmpty)
                        _tag(context, shop.countryTag),
                      if (shop.isVerified)
                        _tag(context, strings.tr('Verifie', 'Verified')),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            InkWell(
              onTap: () => context.push('/shops/${shop.id}/products'),
              borderRadius: DmRadius.r12,
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: DmColors.doorBlue,
                  borderRadius: DmRadius.r12,
                ),
                child: Text(strings.tr('Voir', 'View'),
                    style: const TextStyle(color: Colors.white, fontSize: 12)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _tag(BuildContext context, String label) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = DmColors.iconBg(isDark);
    final fg = DmColors.iconFg(isDark);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: DmRadius.r12,
        border: Border.all(color: DmColors.border(isDark)),
      ),
      child: Text(label,
          style:
              TextStyle(color: fg, fontSize: 11, fontWeight: FontWeight.w600)),
    );
  }
}

