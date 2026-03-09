import 'package:flutter/material.dart';

import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';

class PaymentsScreen extends StatefulWidget {
  const PaymentsScreen({super.key});

  @override
  State<PaymentsScreen> createState() => _PaymentsScreenState();
}

class _PaymentsScreenState extends State<PaymentsScreen> {
  String _method = 'Stripe';
  final _prepaidController = TextEditingController();

  static final List<_PaymentMethodView> _methods = [
    const _PaymentMethodView(
      id: 'Stripe',
      title: 'Stripe',
      subtitleFr: 'Carte bancaire internationale',
      subtitleEn: 'International bank cards',
      active: true,
      etaFr: 'Actif',
      etaEn: 'Live',
    ),
    const _PaymentMethodView(
      id: 'PayPal',
      title: 'PayPal',
      subtitleFr: 'Paiement securise en ligne',
      subtitleEn: 'Secure online payment',
      active: true,
      etaFr: 'Actif',
      etaEn: 'Live',
    ),
    const _PaymentMethodView(
      id: 'PrepaidCard',
      title: 'DoorMarket Prepayee',
      subtitleFr: 'Code prepayee pour payer immediatement',
      subtitleEn: 'Prepaid code for instant payment',
      active: true,
      etaFr: 'Actif',
      etaEn: 'Live',
    ),
    const _PaymentMethodView(
      id: 'MobileMoney',
      title: 'Mobile Money',
      subtitleFr: 'Airtel Money / Orange Money',
      subtitleEn: 'Airtel Money / Orange Money',
      active: false,
      etaFr: 'Bientot',
      etaEn: 'Soon',
    ),
    const _PaymentMethodView(
      id: 'CashOnDelivery',
      title: 'Paiement a la livraison',
      subtitleFr: 'Disponible selon la zone',
      subtitleEn: 'Available by delivery zone',
      active: false,
      etaFr: 'Bientot',
      etaEn: 'Soon',
    ),
  ];

  @override
  void dispose() {
    _prepaidController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final activeMethods = _methods.where((m) => m.active).toList();
    final selectedMethodId = activeMethods.any((m) => m.id == _method)
        ? _method
        : (activeMethods.isEmpty ? '' : activeMethods.first.id);

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Paiements', 'Payments'),
            subtitle: t(context, 'Choisissez votre moyen prefere',
                'Choose your preferred method'),
            showBack: true,
            onBack: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 20),
              child: Column(
                children: [
                  DmCard(
                    margin: const EdgeInsets.only(bottom: 10),
                    backgroundColor: isDark
                        ? DmColors.surfaceAltDark
                        : DmColors.surfaceAltLight,
                    child: Row(
                      children: [
                        Container(
                          height: 40,
                          width: 40,
                          decoration: BoxDecoration(
                            color: DmColors.iconBg(isDark),
                            borderRadius: DmRadius.r12,
                          ),
                          child: Icon(
                            Icons.lock_outline,
                            color: isDark
                                ? DmColors.textPrimaryDark
                                : DmColors.doorBlue,
                            size: 18,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            t(
                              context,
                              'Toutes les transactions DoorMarket sont chiffrees et tracees.',
                              'All DoorMarket transactions are encrypted and tracked.',
                            ),
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                        ),
                      ],
                    ),
                  ),
                  for (final method in _methods)
                    _paymentMethodCard(
                      context,
                      method: method,
                      selectedMethodId: selectedMethodId,
                    ),
                  if (selectedMethodId == 'PrepaidCard') ...[
                    const SizedBox(height: 10),
                    DmCard(
                      margin: const EdgeInsets.only(bottom: 10),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            t(context, 'Code prepayee', 'Prepaid code'),
                            style: Theme.of(context).textTheme.bodyMedium,
                          ),
                          const SizedBox(height: 8),
                          TextField(
                            controller: _prepaidController,
                            decoration: InputDecoration(
                              labelText: t(
                                context,
                                'Numero carte prepayee',
                                'Prepaid card number',
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                  DmCard(
                    margin: EdgeInsets.zero,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          t(context, 'Protection client', 'Buyer protection'),
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                        const SizedBox(height: 8),
                        _bullet(
                          context,
                          t(
                            context,
                            'Confirmation de paiement avant validation commande',
                            'Payment confirmation before order validation',
                          ),
                        ),
                        _bullet(
                          context,
                          t(
                            context,
                            'Historique consultable depuis vos commandes',
                            'History available from your orders',
                          ),
                        ),
                        _bullet(
                          context,
                          t(
                            context,
                            'Relance automatique en cas de paiement incomplet',
                            'Automatic reminder for incomplete payments',
                          ),
                        ),
                        const SizedBox(height: 10),
                        DmPrimaryButton(
                          label: t(context, 'Enregistrer', 'Save'),
                          onPressed: () {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text(
                                  t(
                                    context,
                                    'Moyen de paiement mis a jour.',
                                    'Payment method updated.',
                                  ),
                                ),
                              ),
                            );
                          },
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _paymentMethodCard(
    BuildContext context, {
    required _PaymentMethodView method,
    required String selectedMethodId,
  }) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final selected = method.active && selectedMethodId == method.id;
    final bg = selected
        ? DmColors.infoSurface(isDark)
        : (isDark ? DmColors.surfaceDark : DmColors.surfaceLight);
    final border = selected
        ? DmColors.doorBlue
        : (isDark ? DmColors.borderDark : DmColors.borderLight);

    return InkWell(
      onTap: method.active ? () => setState(() => _method = method.id) : null,
      borderRadius: DmRadius.r16,
      child: DmCard(
        margin: const EdgeInsets.only(bottom: 10),
        backgroundColor: bg,
        borderColor: border,
        child: Row(
          children: [
            SizedBox(
              height: 42,
              width: 76,
              child: Container(
                decoration: BoxDecoration(
                  color: isDark
                      ? DmColors.surfaceAltDark
                      : DmColors.surfaceAltLight,
                  borderRadius: DmRadius.r12,
                  border: Border.all(color: DmColors.border(isDark)),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 6),
                alignment: Alignment.center,
                child: Image.asset(
                  _paymentAsset(method.id),
                  fit: BoxFit.contain,
                  errorBuilder: (_, __, ___) => Icon(
                    _paymentIcon(method.id),
                    size: 20,
                    color: DmColors.iconFg(isDark),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    method.title,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    t(context, method.subtitleFr, method.subtitleEn),
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
              decoration: BoxDecoration(
                color: method.active
                    ? DmColors.successSurface(isDark)
                    : DmColors.warningSurface(isDark),
                borderRadius: DmRadius.r12,
              ),
              child: Text(
                t(context, method.etaFr, method.etaEn),
                style: TextStyle(
                  color: method.active
                      ? (isDark ? DmColors.successDark : DmColors.successLight)
                      : DmColors.warningLight,
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _bullet(BuildContext context, String text) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: EdgeInsets.only(top: 5),
            child: Icon(
              Icons.circle,
              size: 6,
              color: DmColors.iconFg(isDark),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.bodySmall,
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

  static IconData _paymentIcon(String id) {
    switch (id) {
      case 'Stripe':
        return Icons.credit_card;
      case 'PayPal':
        return Icons.account_balance_wallet_outlined;
      case 'PrepaidCard':
        return Icons.confirmation_number_outlined;
      case 'MobileMoney':
        return Icons.phone_android_outlined;
      case 'CashOnDelivery':
        return Icons.local_shipping_outlined;
      default:
        return Icons.payments_outlined;
    }
  }

  static String _paymentAsset(String id) {
    switch (id) {
      case 'Stripe':
        return 'assets/images/payment_stripe.png';
      case 'PayPal':
        return 'assets/images/payment_paypal.png';
      case 'PrepaidCard':
        return 'assets/images/payment_prepaid.png';
      case 'MobileMoney':
        return 'assets/images/payment_mobile_money.png';
      case 'CashOnDelivery':
        return 'assets/images/payment_cod.png';
      default:
        return 'assets/images/payment_prepaid.png';
    }
  }
}

class _PaymentMethodView {
  final String id;
  final String title;
  final String subtitleFr;
  final String subtitleEn;
  final bool active;
  final String etaFr;
  final String etaEn;

  const _PaymentMethodView({
    required this.id,
    required this.title,
    required this.subtitleFr,
    required this.subtitleEn,
    required this.active,
    required this.etaFr,
    required this.etaEn,
  });
}
