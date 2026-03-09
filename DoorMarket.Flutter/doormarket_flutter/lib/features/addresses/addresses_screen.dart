import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/me.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_secondary_button.dart';
import 'addresses_provider.dart';

class AddressesScreen extends ConsumerWidget {
  const AddressesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(addressesProvider);
    final t = _tr;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Mes adresses', 'My addresses'),
            subtitle: t(context, 'Vos adresses de livraison', 'Your delivery addresses'),
            showBack: true,
            showLanguageBadge: false,
            showAction: true,
            actionIcon: Icons.add,
            onAction: () => context.push('/addresses/new'),
            onBack: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: data.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (err, _) => Center(child: Text(t(context, 'Erreur: $err', 'Error: $err'))),
              data: (addresses) => ListView(
                padding: const EdgeInsets.all(24),
                children: [
                  if (addresses.isEmpty) Text(t(context, 'Aucune adresse', 'No address')),
                  ...addresses.map((address) => _addressCard(context, ref, address)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _addressCard(BuildContext context, WidgetRef ref, AddressDto address) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(address.label, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          Text(
            address.fullName,
            style: TextStyle(color: DmColors.mutedText(isDark), fontSize: 12),
          ),
          const SizedBox(height: 4),
          Text(
            address.phone,
            style: TextStyle(color: DmColors.mutedText(isDark), fontSize: 12),
          ),
          const SizedBox(height: 4),
          Text(
            address.street,
            style: TextStyle(color: DmColors.mutedText(isDark), fontSize: 12),
          ),
          const SizedBox(height: 4),
          Text(
            address.city,
            style: TextStyle(color: DmColors.mutedText(isDark), fontSize: 12),
          ),
          if (address.isDefault) ...[
            const SizedBox(height: 6),
            Text(t(context, 'Adresse par defaut', 'Default address'),
                style: const TextStyle(color: DmColors.doorOrange, fontSize: 12)),
          ],
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: DmSecondaryButton(
                  height: 36,
                  label: t(context, 'Modifier', 'Edit'),
                  onPressed: () => context.push('/addresses/${address.id}'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: SizedBox(
                  height: 36,
                  child: OutlinedButton(
                    style: OutlinedButton.styleFrom(
                      foregroundColor:
                          isDark ? DmColors.errorDark : DmColors.errorLight,
                      side: BorderSide(
                        color: isDark ? DmColors.errorDark : DmColors.errorLight,
                      ),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    onPressed: () => _confirmDelete(context, ref, address),
                    child: Text(t(context, 'Supprimer', 'Delete')),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref, AddressDto address) async {
    final t = _tr;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(t(context, 'Supprimer', 'Delete')),
        content: Text(t(context, 'Supprimer cette adresse ?', 'Delete this address?')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(t(context, 'Annuler', 'Cancel'))),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: Text(t(context, 'Supprimer', 'Delete'))),
        ],
      ),
    );
    if (ok != true) return;
    await ref.read(addressApiProvider).delete(address.id);
    ref.invalidate(addressesProvider);
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr = Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
