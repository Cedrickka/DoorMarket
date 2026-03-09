import 'package:flutter/material.dart';

import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_brand_mark.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';

class AboutScreen extends StatelessWidget {
  const AboutScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'A propos', 'About'),
            subtitle: t(context, 'Decouvrez DoorMarket', 'Discover DoorMarket'),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(DmSpacing.xxl),
              child: Column(
                children: [
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Container(
                              height: 48,
                              width: 48,
                              decoration: BoxDecoration(
                                color: DmColors.orangeSoftBgLight,
                                borderRadius: DmRadius.r16,
                              ),
                              padding: const EdgeInsets.all(6),
                              child: const DmBrandMark(size: 36),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                'DoorMarket',
                                style: Theme.of(context).textTheme.headlineSmall,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Text(
                          t(
                            context,
                            'DoorMarket permet de commander facilement des produits de plusieurs boutiques, avec livraison rapide et suivi en temps reel.',
                            'DoorMarket helps you order products from multiple shops with fast delivery and real-time tracking.',
                          ),
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                        const SizedBox(height: 16),
                        _bullet(context, t(context, 'Catalogue varie et prix transparents', 'Wide catalog and transparent pricing')),
                        _bullet(context, t(context, 'Paiement securise et suivi commande', 'Secure payment and order tracking')),
                        _bullet(context, t(context, 'Livraison a votre porte', 'Delivery to your door')),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(t(context, 'Notre mission', 'Our mission'), style: Theme.of(context).textTheme.bodyMedium),
                        const SizedBox(height: 6),
                        Text(
                          t(
                            context,
                            'Relier les boutiques locales aux clients en offrant une experience simple, rapide et fiable.',
                            'Connect local shops to customers with a simple, fast, and reliable experience.',
                          ),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                  DmPrimaryButton(
                    label: t(context, 'Voir nos boutiques', 'See our shops'),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _bullet(BuildContext context, String text) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          const Icon(Icons.check_circle, color: DmColors.successLight, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: Theme.of(context).textTheme.bodySmall)),
        ],
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr = Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
