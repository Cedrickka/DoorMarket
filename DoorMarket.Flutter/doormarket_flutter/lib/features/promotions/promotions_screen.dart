import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/products.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_badge_promo.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';

final promotionsProvider = FutureProvider<List<ProductDto>>((ref) async {
  final api = ref.read(productsApiProvider);
  final result =
      await api.search(const ProductQuery(promotedOnly: true, pageSize: 50));
  return result.items;
});

class PromotionsScreen extends ConsumerWidget {
  const PromotionsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = _tr;
    final data = ref.watch(promotionsProvider);
    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Promotions', 'Promotions'),
            subtitle: t(context, 'Offres en cours', 'Current offers'),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
          ),
          Expanded(
            child: data.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (err, _) => Center(
                child: Text(t(context, 'Erreur: $err', 'Error: $err')),
              ),
              data: (items) => RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(promotionsProvider);
                  await ref.read(promotionsProvider.future);
                },
                child: items.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(16),
                        children: [
                          DmCard(
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
                                  child: const Icon(Icons.local_offer_outlined,
                                      color: DmColors.doorOrange),
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Text(
                                    t(
                                      context,
                                      'Aucune promotion active pour le moment.',
                                      'No active promotions at the moment.',
                                    ),
                                    style:
                                        Theme.of(context).textTheme.bodySmall,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      )
                    : GridView.builder(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(16),
                        cacheExtent: 900,
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 2,
                          crossAxisSpacing: 6,
                          mainAxisSpacing: 6,
                          childAspectRatio: 0.76,
                        ),
                        itemCount: items.length,
                        itemBuilder: (context, index) =>
                            _promoCard(context, items[index]),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _promoCard(BuildContext context, ProductDto product) {
    return InkWell(
      onTap: () => context.push('/product/${product.id}'),
      child: DmCard(
        margin: EdgeInsets.zero,
        padding: const EdgeInsets.all(10),
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
                      top: 6,
                      left: 6,
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
            Text(product.shopName,
                style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 6),
            Text(
              '${product.currency} ${product.effectivePrice.toStringAsFixed(2)}',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: DmColors.doorOrange,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            if (product.hasActivePromotion)
              Text(
                '${product.currency} ${product.price.toStringAsFixed(2)}',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      decoration: TextDecoration.lineThrough,
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
