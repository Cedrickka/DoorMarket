import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';

class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  @override
  Widget build(BuildContext context) {
    final themeController = ref.watch(themeControllerProvider);
    final currentLocale = ref.watch(localeControllerProvider);
    final notificationPrefs = ref.watch(notificationPreferencesProvider);
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Parametres', 'Settings'),
            subtitle: t(context, 'Langue, apparence et notifications',
                'Language, appearance and notifications'),
            showBack: true,
            onBack: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: [
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Langue', 'Language'),
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<String>(
                        key: ValueKey(currentLocale?.languageCode ?? 'system'),
                        initialValue: currentLocale?.languageCode ?? 'system',
                        items: [
                          DropdownMenuItem(
                              value: 'fr',
                              child: Text(t(context, 'Francais', 'French'))),
                          const DropdownMenuItem(
                              value: 'en', child: Text('English')),
                          DropdownMenuItem(
                              value: 'system',
                              child: Text(t(context, 'Systeme', 'System'))),
                        ],
                        onChanged: (value) {
                          if (value == null || value == 'system') {
                            ref
                                .read(localeControllerProvider.notifier)
                                .setLocale(null);
                          } else {
                            ref
                                .read(localeControllerProvider.notifier)
                                .setLocale(value);
                          }
                        },
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Apparence', 'Appearance'),
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 8),
                      SwitchListTile.adaptive(
                        contentPadding: EdgeInsets.zero,
                        title: Text(t(context, 'Suivre le theme systeme',
                            'Follow system theme')),
                        value: themeController.useSystem,
                        onChanged: (value) =>
                            themeController.setUseSystem(value),
                      ),
                      if (!themeController.useSystem)
                        SwitchListTile.adaptive(
                          contentPadding: EdgeInsets.zero,
                          title: Text(t(context, 'Mode sombre', 'Dark mode')),
                          value: themeController.themeMode == ThemeMode.dark,
                          onChanged: (value) =>
                              themeController.setDarkMode(value),
                        ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Notifications', 'Notifications'),
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 8),
                      SwitchListTile.adaptive(
                        contentPadding: EdgeInsets.zero,
                        title: Text(t(context, 'Activer les notifications',
                            'Enable notifications')),
                        value: notificationPrefs.enabled,
                        onChanged: (value) =>
                            notificationPrefs.setEnabled(value),
                      ),
                      SwitchListTile.adaptive(
                        contentPadding: EdgeInsets.zero,
                        title: Text(t(
                            context, 'Mises a jour commande', 'Order updates')),
                        value: notificationPrefs.ordersEnabled,
                        onChanged: notificationPrefs.enabled
                            ? (value) =>
                                notificationPrefs.setOrdersEnabled(value)
                            : null,
                      ),
                      SwitchListTile.adaptive(
                        contentPadding: EdgeInsets.zero,
                        title: Text(
                            t(context, 'Alertes paiement', 'Payment alerts')),
                        value: notificationPrefs.paymentsEnabled,
                        onChanged: notificationPrefs.enabled
                            ? (value) =>
                                notificationPrefs.setPaymentsEnabled(value)
                            : null,
                      ),
                      SwitchListTile.adaptive(
                        contentPadding: EdgeInsets.zero,
                        title: Text(
                            t(context, 'Alertes livraison', 'Delivery alerts')),
                        value: notificationPrefs.deliveryEnabled,
                        onChanged: notificationPrefs.enabled
                            ? (value) =>
                                notificationPrefs.setDeliveryEnabled(value)
                            : null,
                      ),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              t(
                                context,
                                'Historique notifications lues: ${notificationPrefs.readEventsCount}',
                                'Read notification history: ${notificationPrefs.readEventsCount}',
                              ),
                              style: TextStyle(
                                fontSize: 12,
                                color: DmColors.mutedText(isDark),
                              ),
                            ),
                          ),
                          TextButton(
                            onPressed: notificationPrefs.readEventsCount == 0
                                ? null
                                : () => notificationPrefs.clearReadEvents(),
                            child: Text(t(context, 'Reinitialiser', 'Reset')),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Version', 'Version'),
                          style: const TextStyle(fontWeight: FontWeight.w600)),
                      const SizedBox(height: 8),
                      Text(
                        'DoorMarket Mobile 1.0.0',
                        style: TextStyle(color: DmColors.mutedText(isDark)),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
