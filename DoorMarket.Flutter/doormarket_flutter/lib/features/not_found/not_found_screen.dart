import 'package:flutter/material.dart';

import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_brand_mark.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_primary_button.dart';

class NotFoundScreen extends StatelessWidget {
  const NotFoundScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(DmSpacing.xxl),
          child: DmCard(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  height: 96,
                  width: 96,
                  decoration: BoxDecoration(
                    color: DmColors.surfaceAltLight,
                    borderRadius: DmRadius.r20,
                  ),
                  padding: const EdgeInsets.all(16),
                  child: const DmBrandMark(size: 56),
                ),
                const SizedBox(height: 16),
                Text('404', style: Theme.of(context).textTheme.headlineLarge?.copyWith(color: DmColors.doorOrange)),
                const SizedBox(height: 6),
                Text(t(context, 'Page introuvable', 'Page not found'), style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 12),
                Text(
                  t(
                    context,
                    'Cette page n existe pas ou a ete deplacee. Retournez a l accueil pour continuer.',
                    'This page does not exist or has been moved. Return to home to continue.',
                  ),
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                const SizedBox(height: 18),
                DmPrimaryButton(label: t(context, 'Retour a l accueil', 'Back to home'), onPressed: () => Navigator.of(context).pop()),
              ],
            ),
          ),
        ),
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr = Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
