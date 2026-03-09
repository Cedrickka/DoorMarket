import 'package:flutter/material.dart';
import '../../core/theme/theme.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_ghost_button.dart';
import '../../core/widgets/dm_chip.dart';
import '../../core/widgets/dm_search_bar.dart';
import '../../core/widgets/dm_badge_promo.dart';

class UiGalleryScreen extends StatelessWidget {
  const UiGalleryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    return Scaffold(
      appBar: AppBar(title: Text(t(context, 'Galerie UI', 'UI Gallery'))),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(t(context, 'Theme clair', 'Light theme'), style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 12),
          Theme(
            data: DmTheme.light(),
            child: _galleryContent(context),
          ),
          const SizedBox(height: 24),
          Text(t(context, 'Theme sombre', 'Dark theme'), style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 12),
          Theme(
            data: DmTheme.dark(),
            child: Container(
              padding: const EdgeInsets.all(16),
              color: DmTheme.dark().scaffoldBackgroundColor,
              child: _galleryContent(context),
            ),
          ),
        ],
      ),
    );
  }

  Widget _galleryContent(BuildContext context) {
    final t = _tr;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        DmSearchBar(hint: t(context, 'Rechercher...', 'Search...')),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          children: [
            DmChip(label: t(context, 'Fruits', 'Fruits')),
            DmChip(label: t(context, 'Legumes', 'Vegetables')),
            DmChip(label: t(context, 'Promos', 'Promos'), selected: true),
          ],
        ),
        const SizedBox(height: 12),
        DmCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('DmCard', style: DmTheme.light().textTheme.headlineSmall),
              const SizedBox(height: 8),
              Text(t(context, 'Carte avec bordure et ombre douces.', 'Card with soft border and shadow.')),
              const SizedBox(height: 12),
              Row(
                children: [
                  DmPrimaryButton(label: t(context, 'Principal', 'Primary')),
                ],
              ),
              const SizedBox(height: 8),
              DmGhostButton(label: t(context, 'Secondaire', 'Ghost')),
            ],
          ),
        ),
        const SizedBox(height: 12),
        DmCard(
          child: Stack(
            children: const [
              SizedBox(height: 120),
              Positioned(top: 8, right: 8, child: DmBadgePromo(label: '-30%')),
            ],
          ),
        ),
      ],
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr = Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
