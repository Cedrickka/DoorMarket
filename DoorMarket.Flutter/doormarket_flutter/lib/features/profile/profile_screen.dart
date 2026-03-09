import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/me.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import '../../core/theme/theme_controller.dart';
import '../notifications/notification_counters_provider.dart';
import 'profile_provider.dart';

final loyaltyWalletProvider = FutureProvider((ref) async {
  return ref.read(loyaltyApiProvider).getMyWallet();
});

final loyaltyHistoryProvider = FutureProvider((ref) async {
  return ref.read(loyaltyApiProvider).getMyHistory(page: 1, pageSize: 5);
});

class ProfileScreen extends ConsumerStatefulWidget {
  final ThemeController themeController;
  final VoidCallback onNotifications;
  final VoidCallback onAbout;
  final VoidCallback onUiGallery;
  final VoidCallback onAddresses;
  final VoidCallback onWishlist;
  final VoidCallback onReturns;
  final VoidCallback onPayments;
  final VoidCallback onSupport;
  final VoidCallback onSettings;

  const ProfileScreen({
    super.key,
    required this.themeController,
    required this.onNotifications,
    required this.onAbout,
    required this.onUiGallery,
    required this.onAddresses,
    required this.onWishlist,
    required this.onReturns,
    required this.onPayments,
    required this.onSupport,
    required this.onSettings,
  });

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _saving = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final data = ref.watch(meProvider);
    final unreadNotifications = ref.watch(unreadNotificationsCountProvider);
    final unreadNotificationsCount =
        unreadNotifications.maybeWhen(data: (value) => value, orElse: () => 0);

    return Scaffold(
      body: data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) =>
            Center(child: Text(t(context, 'Erreur: $err', 'Error: $err'))),
        data: (me) => _content(context, ref, me,
            unreadNotificationsCount: unreadNotificationsCount),
      ),
    );
  }

  Widget _content(
    BuildContext context,
    WidgetRef ref,
    MeDto me, {
    required int unreadNotificationsCount,
  }) {
    final t = _tr;
    if (_phoneController.text.isEmpty) {
      _phoneController.text = me.phone ?? '';
    }
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final loyaltyWallet = ref.watch(loyaltyWalletProvider);
    final loyaltyHistory = ref.watch(loyaltyHistoryProvider);

    return Column(
      children: [
        DmHeader(
          title: t(context, 'Profil', 'Profile'),
          subtitle:
              t(context, 'Informations personnelles', 'Personal information'),
        ),
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DmCard(
                  child: Row(
                    children: [
                      GestureDetector(
                        onTap: () => _uploadPhoto(context, ref),
                        child: CircleAvatar(
                          radius: 36,
                          backgroundColor: isDark
                              ? DmColors.iconBgDark
                              : DmColors.iconBgLight,
                          backgroundImage: me.profileImageUrl != null &&
                                  me.profileImageUrl!.isNotEmpty
                              ? NetworkImage(me.profileImageUrl!)
                              : null,
                          child: me.profileImageUrl == null ||
                                  me.profileImageUrl!.isEmpty
                              ? Icon(
                                  Icons.person,
                                  color: isDark
                                      ? DmColors.textPrimaryDark
                                      : DmColors.doorBlue,
                                )
                              : null,
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(t(context, 'Compte', 'Account'),
                                style:
                                    Theme.of(context).textTheme.headlineSmall),
                            const SizedBox(height: 4),
                            Text(me.email,
                                style: Theme.of(context).textTheme.bodySmall),
                            const SizedBox(height: 8),
                            DmSecondaryButton(
                              height: 44,
                              label:
                                  t(context, 'Changer photo', 'Change photo'),
                              onPressed: () => _uploadPhoto(context, ref),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Informations', 'Information'),
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _phoneController,
                        decoration: InputDecoration(
                            labelText: t(context, 'Telephone', 'Phone')),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _passwordController,
                        obscureText: true,
                        decoration: InputDecoration(
                            labelText: t(context, 'Nouveau mot de passe',
                                'New password')),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _confirmController,
                        obscureText: true,
                        decoration: InputDecoration(
                            labelText: t(context, 'Confirmer le mot de passe',
                                'Confirm password')),
                      ),
                      const SizedBox(height: 12),
                      _infoRow(t(context, 'Role', 'Role'), me.role),
                      const SizedBox(height: 6),
                      _infoRow(
                          t(context, 'Statut', 'Status'),
                          me.isActive
                              ? t(context, 'Actif', 'Active')
                              : t(context, 'Inactif', 'Inactive')),
                      if (_error != null) ...[
                        const SizedBox(height: 8),
                        Text(
                          _error!,
                          style: TextStyle(
                            color:
                                isDark ? DmColors.errorDark : DmColors.errorLight,
                          ),
                        ),
                      ],
                      const SizedBox(height: 12),
                      DmPrimaryButton(
                        label: _saving
                            ? t(context, 'Sauvegarde...', 'Saving...')
                            : t(context, 'Enregistrer', 'Save'),
                        onPressed: _saving
                            ? null
                            : () => _saveProfile(context, ref, me),
                      ),
                      const SizedBox(height: 10),
                      DmSecondaryButton(
                        label: t(
                            context, 'Changer mot de passe', 'Change password'),
                        onPressed: _saving
                            ? null
                            : () => _changePassword(context, ref),
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(t(context, 'Fidelite', 'Loyalty'),
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 10),
                      loyaltyWallet.when(
                        loading: () =>
                            const LinearProgressIndicator(minHeight: 2),
                        error: (err, _) => Text(
                          t(context, 'Erreur fidelite: $err',
                              'Loyalty error: $err'),
                          style: TextStyle(
                            color:
                                isDark ? DmColors.errorDark : DmColors.errorLight,
                          ),
                        ),
                        data: (wallet) => Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              '${wallet.pointsBalance} pts',
                              style: const TextStyle(
                                fontSize: 24,
                                fontWeight: FontWeight.w800,
                                color: DmColors.doorOrange,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              '${wallet.walletCurrency} ${wallet.walletValue.toStringAsFixed(2)}',
                              style: TextStyle(
                                color: DmColors.mutedText(isDark),
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              t(context,
                                  'Gagnes: ${wallet.lifetimeEarned} | Utilises: ${wallet.lifetimeSpent}',
                                  'Earned: ${wallet.lifetimeEarned} | Spent: ${wallet.lifetimeSpent}'),
                              style: const TextStyle(fontSize: 12),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 10),
                      loyaltyHistory.when(
                        loading: () => const SizedBox.shrink(),
                        error: (_, __) => const SizedBox.shrink(),
                        data: (rows) => Column(
                          children: rows
                              .take(3)
                              .map(
                                (row) => Padding(
                                  padding: const EdgeInsets.only(bottom: 6),
                                  child: Row(
                                    children: [
                                      Expanded(
                                        child: Text(
                                          row.note.isEmpty
                                              ? row.sourceType
                                              : row.note,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: const TextStyle(fontSize: 12),
                                        ),
                                      ),
                                      Text(
                                        row.pointsDelta >= 0
                                            ? '+${row.pointsDelta}'
                                            : '${row.pointsDelta}',
                                        style: TextStyle(
                                          fontSize: 12,
                                          fontWeight: FontWeight.w700,
                                          color: row.pointsDelta >= 0
                                              ? (isDark
                                                  ? DmColors.successDark
                                                  : DmColors.successLight)
                                              : (isDark
                                                  ? DmColors.errorDark
                                                  : DmColors.errorLight),
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              )
                              .toList(),
                        ),
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: Column(
                    children: [
                      _menuItem(
                          context,
                          Icons.location_on_outlined,
                          t(context, 'Mes adresses', 'My addresses'),
                          widget.onAddresses),
                      _divider(context),
                      _menuItem(
                          context,
                          Icons.notifications_none,
                          t(context, 'Notifications', 'Notifications'),
                          widget.onNotifications,
                          badgeCount: unreadNotificationsCount),
                      _divider(context),
                      _menuItem(
                          context,
                          Icons.favorite_border,
                          t(context, 'Wishlist', 'Wishlist'),
                          widget.onWishlist),
                      _divider(context),
                      _menuItem(
                          context,
                          Icons.assignment_return_outlined,
                          t(context, 'Retours', 'Returns'),
                          widget.onReturns),
                      _divider(context),
                      _menuItem(
                          context,
                          Icons.payment,
                          t(context, 'Paiements', 'Payments'),
                          widget.onPayments),
                      _divider(context),
                      _menuItem(context, Icons.support_agent,
                          t(context, 'Support', 'Support'), widget.onSupport),
                      _divider(context),
                      _menuItem(
                          context,
                          Icons.settings,
                          t(context, 'Parametres', 'Settings'),
                          widget.onSettings),
                    ],
                  ),
                ),
                DmSecondaryButton(
                  label: t(context, 'Deconnexion', 'Logout'),
                  onPressed: () => _logout(context, ref),
                ),
                if (_saving) ...[
                  const SizedBox(height: 12),
                  const Center(child: CircularProgressIndicator()),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _infoRow(String label, String value) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
        Text(value),
      ],
    );
  }

  Widget _menuItem(
    BuildContext context,
    IconData icon,
    String label,
    VoidCallback onTap, {
    int badgeCount = 0,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Container(
        height: 38,
        width: 38,
        decoration: BoxDecoration(
          color: isDark ? DmColors.iconBgDark : DmColors.iconBgLight,
          borderRadius: DmRadius.r12,
        ),
        child: Icon(
          icon,
          size: 20,
          color: DmColors.iconFg(isDark),
        ),
      ),
      title: Text(label),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (badgeCount > 0)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
              decoration: BoxDecoration(
                color: DmColors.doorOrange,
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                badgeCount > 99 ? '99+' : '$badgeCount',
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          if (badgeCount > 0) const SizedBox(width: 8),
          Icon(
            Icons.chevron_right,
            color: isDark ? DmColors.textMutedDark : DmColors.textMutedLight,
          ),
        ],
      ),
      onTap: onTap,
    );
  }

  Widget _divider(BuildContext context) {
    return Divider(
      height: 1,
      color: Theme.of(context).brightness == Brightness.dark
          ? DmColors.borderDark
          : DmColors.borderLight,
    );
  }

  Future<void> _uploadPhoto(BuildContext context, WidgetRef ref) async {
    final result = await FilePicker.platform
        .pickFiles(type: FileType.image, withData: true);
    if (result == null || result.files.isEmpty) return;

    final file = result.files.first;
    final bytes = file.bytes;
    if (bytes == null) return;

    final api = ref.read(meApiProvider);
    final ext = (file.extension ?? '').toLowerCase();
    final contentType = ext == 'png'
        ? 'image/png'
        : ext == 'webp'
            ? 'image/webp'
            : 'image/jpeg';
    await api.uploadProfilePhoto(
      bytes: bytes,
      fileName: file.name,
      contentType: contentType,
    );
    ref.invalidate(meProvider);
  }

  Future<void> _saveProfile(
      BuildContext context, WidgetRef ref, MeDto me) async {
    final t = _tr;
    setState(() {
      _saving = true;
      _error = null;
    });
    final api = ref.read(meApiProvider);
    final phone = _phoneController.text.trim();
    if (phone.isEmpty || phone == (me.phone ?? '')) {
      setState(() => _saving = false);
      return;
    }
    try {
      await api.requestPhoneChange(RequestPhoneChangeRequest(phone: phone));
      if (!context.mounted) return;
      final code = await _promptText(
          context, t(context, 'Code OTP', 'OTP code'), TextEditingController());
      if (!context.mounted) return;
      if (code == null || code.isEmpty) return;
      await api.confirmPhoneChange(ConfirmPhoneChangeRequest(code: code));
      if (!context.mounted) return;
      ref.invalidate(meProvider);
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _changePassword(BuildContext context, WidgetRef ref) async {
    final t = _tr;
    final passwordController = TextEditingController();
    final codeController = TextEditingController();
    final api = ref.read(meApiProvider);

    final password = _passwordController.text.trim().isNotEmpty
        ? _passwordController.text.trim()
        : await _promptText(
            context,
            t(context, 'Nouveau mot de passe', 'New password'),
            passwordController,
            obscure: true);
    if (password == null || password.isEmpty) return;
    if (_confirmController.text.isNotEmpty &&
        _confirmController.text.trim() != password) {
      setState(() => _error = t(
          context,
          'Les mots de passe ne correspondent pas.',
          'Passwords do not match.'));
      return;
    }

    await api.requestPasswordChange(
        RequestPasswordChangeRequest(password: password));
    if (!context.mounted) return;

    final code = await _promptText(
        context, t(context, 'Code OTP', 'OTP code'), codeController);
    if (!context.mounted) return;
    if (code == null || code.isEmpty) return;

    await api.confirmPasswordChange(ConfirmPasswordChangeRequest(code: code));
  }

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    await ref.read(authControllerProvider.notifier).logout();
    if (!context.mounted) return;
    Navigator.of(context).pop();
  }

  Future<String?> _promptText(
    BuildContext context,
    String title,
    TextEditingController controller, {
    bool obscure = false,
  }) async {
    final t = _tr;
    return showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: TextField(
          controller: controller,
          obscureText: obscure,
          decoration: const InputDecoration(border: OutlineInputBorder()),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: Text(t(context, 'Annuler', 'Cancel'))),
          TextButton(
              onPressed: () => Navigator.pop(ctx, controller.text.trim()),
              child: Text(t(context, 'Confirmer', 'Confirm'))),
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

