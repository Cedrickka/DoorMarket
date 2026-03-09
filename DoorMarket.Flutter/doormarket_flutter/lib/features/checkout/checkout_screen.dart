import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:flutter_tts/flutter_tts.dart';

import '../../core/models/cart.dart';
import '../../core/models/me.dart';
import '../../core/models/orders.dart';
import '../../core/models/payments.dart';
import '../../core/providers.dart';
import '../../core/services/checkout_hardening_service.dart';
import '../../core/services/user_error_service.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import '../orders/orders_provider.dart';

class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen>
    with AutomaticKeepAliveClientMixin {
  final FlutterTts _tts = FlutterTts();
  final _promoController = TextEditingController();
  bool _loading = true;
  bool _submittingOrder = false;
  CartDto? _cart;
  List<AddressDto> _addresses = [];
  AddressDto? _selected;
  String _payment = 'PayPal';
  String? _promoCode;
  bool _promoApplying = false;
  bool _promoApplied = false;
  String? _promoMessage;
  String _prepaidCode = '';
  String _mobileMoneyProvider = 'AIRTEL';
  String _mobileMoneyPhone = '';
  double _deliveryFee = 0;
  double _discount = 0;
  String _currency = 'USD';
  String? _error;
  bool _checkoutViewTracked = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _tts.stop();
    _promoController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _promoCode = null;
      _promoApplied = false;
      _promoMessage = null;
      _discount = 0;
    });
    _promoController.clear();
    try {
      final cart = await ref.read(cartApiProvider).getMyCart();
      final addresses = await ref.read(addressApiProvider).getAddresses();
      _cart = cart;
      _currency = cart.currency;
      _addresses = addresses;
      _selected = addresses.isNotEmpty
          ? addresses.firstWhere((a) => a.isDefault,
              orElse: () => addresses.first)
          : null;
      if (_selected?.deliveryZoneId != null) {
        final quote = await ref.read(deliveryApiProvider).getQuote(
              subtotal: cart.subtotal,
              currency: _currency,
              zoneId: _selected?.deliveryZoneId,
            );
        _deliveryFee = quote.deliveryFee;
      }
    } catch (e) {
      _error = mounted
          ? UserErrorService.message(
              context,
              e,
              fallbackFr: 'Le chargement du checkout a echoue.',
              fallbackEn: 'Checkout loading failed.',
            )
          : e.toString();
    } finally {
      if (mounted) setState(() => _loading = false);
      if (!_checkoutViewTracked) {
        _checkoutViewTracked = true;
        await _trackCheckoutEvent(
          eventName: 'checkout_viewed',
          paymentProvider: _payment,
          paymentChannel: _resolvePaymentChannel(_payment),
          success: _error == null,
          errorMessage: _error,
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final iconBg = DmColors.iconBg(isDark);
    final mutedText = DmColors.mutedText(isDark);
    final borderColor = DmColors.border(isDark);
    final cart = _cart;
    final subtotal = cart?.subtotal ?? 0;
    final total = (subtotal - _discount) + _deliveryFee;

    return Scaffold(
      backgroundColor: isDark ? DmColors.bgDark : DmColors.bgLight,
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Checkout', 'Checkout'),
            subtitle: t(context, 'Adresse, paiement et confirmation',
                'Address, payment and confirmation'),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
          ),
          Expanded(
            child: Stack(
              children: [
                SingleChildScrollView(
                  key: const PageStorageKey<String>('checkout-scroll'),
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _stepRow(),
                      DmCard(
                        child: Row(
                          children: [
                            _iconBadge(Icons.shopping_cart),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                      t(context, 'Montant a payer',
                                          'Amount to pay'),
                                      style: const TextStyle(
                                          fontWeight: FontWeight.w600)),
                                  const SizedBox(height: 4),
                                  Text(
                                      t(context, 'Total actuel du panier',
                                          'Current cart total'),
                                      style: TextStyle(
                                          color: mutedText, fontSize: 12)),
                                ],
                              ),
                            ),
                            Text(
                              '$_currency ${total.toStringAsFixed(2)}',
                              style: const TextStyle(
                                  fontSize: 18,
                                  fontWeight: FontWeight.w700,
                                  color: DmColors.doorOrange),
                            ),
                          ],
                        ),
                      ),
                      DmCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Text(
                                    t(context, 'Adresse de livraison',
                                        'Delivery address'),
                                    style: Theme.of(context)
                                        .textTheme
                                        .headlineSmall),
                                if (_selected != null)
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 8, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: iconBg,
                                      borderRadius: DmRadius.r12,
                                    ),
                                    child: Text(
                                        t(context, 'Selectionnee', 'Selected'),
                                        style: TextStyle(
                                          fontSize: 11,
                                          color: isDark
                                              ? DmColors.textPrimaryDark
                                              : DmColors.doorBlue,
                                        )),
                                  ),
                              ],
                            ),
                            const SizedBox(height: 12),
                            DropdownButtonFormField<AddressDto>(
                              key: ValueKey(_selected?.id ?? ''),
                              initialValue: _selected,
                              items: _addresses
                                  .map((a) => DropdownMenuItem(
                                      value: a,
                                      child: Text('${a.label} - ${a.city}')))
                                  .toList(),
                              onChanged: (value) async {
                                setState(() => _selected = value);
                                if (value?.deliveryZoneId != null &&
                                    _cart != null) {
                                  final quote = await ref
                                      .read(deliveryApiProvider)
                                      .getQuote(
                                        subtotal: _cart!.subtotal,
                                        currency: _currency,
                                        zoneId: value?.deliveryZoneId,
                                      );
                                  setState(
                                      () => _deliveryFee = quote.deliveryFee);
                                }
                                if ((_promoCode ?? '').trim().isNotEmpty) {
                                  await _applyPromo(showSnack: false);
                                }
                              },
                            ),
                            if (_addresses.isEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                  t(context, 'Aucune adresse disponible',
                                      'No address available'),
                                  style: TextStyle(color: mutedText)),
                            ],
                            const SizedBox(height: 12),
                            DmSecondaryButton(
                              height: 44,
                              label: t(context, 'Gerer les adresses',
                                  'Manage addresses'),
                              onPressed: () => context.push('/addresses'),
                            ),
                            if (_selected != null) ...[
                              const SizedBox(height: 12),
                              Container(
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: iconBg,
                                  borderRadius: DmRadius.r12,
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(_selected!.fullName,
                                        style: const TextStyle(
                                            fontWeight: FontWeight.w600)),
                                    const SizedBox(height: 4),
                                    Text(_selected!.phone,
                                        style: TextStyle(
                                            fontSize: 12, color: mutedText)),
                                    const SizedBox(height: 4),
                                    Text(
                                        '${_selected!.street}, ${_selected!.district}',
                                        style: TextStyle(
                                            fontSize: 12, color: mutedText)),
                                    const SizedBox(height: 4),
                                    Text(
                                        '${_selected!.city}, ${_selected!.country}',
                                        style: TextStyle(
                                            fontSize: 12, color: mutedText)),
                                  ],
                                ),
                              ),
                            ],
                            if (_selected != null &&
                                _selected!.deliveryZoneId == null) ...[
                              const SizedBox(height: 12),
                              Container(
                                padding: const EdgeInsets.all(10),
                                decoration: BoxDecoration(
                                  color: DmColors.warningSurface(isDark),
                                  border:
                                      Border.all(color: DmColors.doorOrange),
                                  borderRadius: DmRadius.r12,
                                ),
                                child: Text(
                                  t(context, 'Zone de livraison requise.',
                                      'Delivery zone is required.'),
                                  style: const TextStyle(
                                      fontSize: 12, color: DmColors.doorOrange),
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                      DmCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(t(context, 'Code promo', 'Promo code'),
                                style:
                                    Theme.of(context).textTheme.headlineSmall),
                            const SizedBox(height: 10),
                            Row(
                              children: [
                                Expanded(
                                  child: TextField(
                                    controller: _promoController,
                                    decoration: InputDecoration(
                                      labelText: t(context, 'Code coupon',
                                          'Coupon code'),
                                      hintText: 'PROMO10',
                                    ),
                                    onChanged: (value) {
                                      setState(() {
                                        _promoCode = value.trim();
                                        if (value.trim().isEmpty) {
                                          _promoApplied = false;
                                          _discount = 0;
                                          _promoMessage = null;
                                        }
                                      });
                                    },
                                  ),
                                ),
                                const SizedBox(width: 8),
                                DmSecondaryButton(
                                  height: 48,
                                  width: 120,
                                  label: _promoApplying
                                      ? '...'
                                      : t(context, 'Appliquer', 'Apply'),
                                  onPressed:
                                      _promoApplying ? null : _applyPromo,
                                ),
                              ],
                            ),
                            if (_promoMessage != null &&
                                _promoMessage!.trim().isNotEmpty) ...[
                              const SizedBox(height: 8),
                              Text(
                                _promoMessage!,
                                style: TextStyle(
                                  fontSize: 12,
                                  color: _promoApplied
                                      ? DmColors.successLight
                                      : DmColors.doorOrange,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                      DmCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(t(context, 'Resume', 'Summary'),
                                style:
                                    Theme.of(context).textTheme.headlineSmall),
                            const SizedBox(height: 12),
                            _summaryRow(t(context, 'Sous-total', 'Subtotal'),
                                '$_currency ${subtotal.toStringAsFixed(2)}'),
                            _summaryRow(t(context, 'Remise', 'Discount'),
                                '$_currency ${_discount.toStringAsFixed(2)}'),
                            _summaryRow(t(context, 'Livraison', 'Delivery'),
                                '$_currency ${_deliveryFee.toStringAsFixed(2)}'),
                            if (_promoApplied &&
                                _promoCode != null &&
                                _promoCode!.trim().isNotEmpty) ...[
                              const SizedBox(height: 6),
                              Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Text(t(context, 'Code promo', 'Promo code'),
                                      style: TextStyle(color: mutedText)),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 8, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: DmColors.doorOrange.withAlpha(31),
                                      borderRadius: DmRadius.r12,
                                    ),
                                    child: Text(_promoCode!,
                                        style: const TextStyle(
                                            color: DmColors.doorOrange,
                                            fontSize: 12)),
                                  ),
                                ],
                              ),
                            ],
                            const Divider(),
                            _summaryRow(t(context, 'Total', 'Total'),
                                '$_currency ${total.toStringAsFixed(2)}',
                                isBold: true),
                          ],
                        ),
                      ),
                      DmCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                _iconBadge(Icons.payments),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(t(context, 'Paiement', 'Payment'),
                                          style: const TextStyle(
                                              fontWeight: FontWeight.w600)),
                                      Text(
                                        _payment == 'Stripe'
                                            ? 'Stripe'
                                            : _payment == 'PayPal'
                                                ? 'PayPal'
                                                : _payment == 'MobileMoney'
                                                    ? t(context, 'Mobile Money',
                                                        'Mobile Money')
                                                    : t(
                                                        context,
                                                        'Carte prepayee',
                                                        'Prepaid card'),
                                        style: TextStyle(
                                            fontSize: 12, color: mutedText),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 12),
                            LayoutBuilder(
                              builder: (context, constraints) {
                                final tileWidth =
                                    (constraints.maxWidth - 10) / 2;
                                return Wrap(
                                  spacing: 10,
                                  runSpacing: 10,
                                  children: [
                                    SizedBox(
                                      width: tileWidth,
                                      child: _paymentOptionCard(
                                        label: 'Stripe',
                                        hint: t(context, 'Carte bancaire',
                                            'Bank card'),
                                        selected: _payment == 'Stripe',
                                        onTap: () =>
                                            _onPaymentMethodSelected('Stripe'),
                                      ),
                                    ),
                                    SizedBox(
                                      width: tileWidth,
                                      child: _paymentOptionCard(
                                        label: 'PayPal',
                                        hint: t(context, 'Paiement securise',
                                            'Secure payment'),
                                        selected: _payment == 'PayPal',
                                        onTap: () =>
                                            _onPaymentMethodSelected('PayPal'),
                                      ),
                                    ),
                                    SizedBox(
                                      width: tileWidth,
                                      child: _paymentOptionCard(
                                        label: t(context, 'Mobile Money',
                                            'Mobile Money'),
                                        hint: t(
                                            context,
                                            'Airtel / Orange / M-Pesa',
                                            'Airtel / Orange / M-Pesa'),
                                        selected: _payment == 'MobileMoney',
                                        onTap: () => _onPaymentMethodSelected(
                                            'MobileMoney'),
                                      ),
                                    ),
                                    SizedBox(
                                      width: tileWidth,
                                      child: _paymentOptionCard(
                                        label:
                                            t(context, 'Prepayee', 'Prepaid'),
                                        hint: t(context, 'Carte prepayee',
                                            'Prepaid card'),
                                        selected: _payment == 'PrepaidCard',
                                        onTap: () => _onPaymentMethodSelected(
                                            'PrepaidCard'),
                                      ),
                                    ),
                                  ],
                                );
                              },
                            ),
                            if (_payment == 'MobileMoney') ...[
                              const SizedBox(height: 12),
                              DropdownButtonFormField<String>(
                                initialValue: _mobileMoneyProvider,
                                decoration: InputDecoration(
                                  labelText:
                                      t(context, 'Operateur', 'Provider'),
                                ),
                                items: const [
                                  DropdownMenuItem(
                                      value: 'AIRTEL',
                                      child: Text('Airtel Money')),
                                  DropdownMenuItem(
                                      value: 'ORANGE',
                                      child: Text('Orange Money')),
                                  DropdownMenuItem(
                                      value: 'MPESA', child: Text('M-Pesa')),
                                ],
                                onChanged: (value) {
                                  if (value == null || value.isEmpty) {
                                    return;
                                  }
                                  setState(() => _mobileMoneyProvider = value);
                                },
                              ),
                              const SizedBox(height: 12),
                              TextField(
                                decoration: InputDecoration(
                                  labelText: t(context, 'Numero Mobile Money',
                                      'Mobile Money number'),
                                  hintText: '+243...',
                                ),
                                onChanged: (value) => _mobileMoneyPhone = value,
                              ),
                            ],
                            if (_payment == 'PrepaidCard') ...[
                              const SizedBox(height: 12),
                              _prepaidPreview(),
                              const SizedBox(height: 12),
                              TextField(
                                decoration: InputDecoration(
                                    labelText: t(
                                        context,
                                        'Numero carte prepayee',
                                        'Prepaid card number')),
                                onChanged: (value) => _prepaidCode = value,
                              ),
                              const SizedBox(height: 6),
                              Text(
                                  t(context, 'Saisissez le code complet.',
                                      'Enter the full code.'),
                                  style: TextStyle(
                                      fontSize: 12, color: mutedText)),
                            ],
                          ],
                        ),
                      ),
                      if (_error != null) ...[
                        const SizedBox(height: 8),
                        Text(_error!,
                            style: TextStyle(
                                color: isDark
                                    ? DmColors.errorDark
                                    : DmColors.errorLight)),
                      ],
                      const SizedBox(height: 80),
                    ],
                  ),
                ),
                if (_loading)
                  Positioned.fill(
                    child: AbsorbPointer(
                      absorbing: true,
                      child: Container(
                        color: Colors.black.withAlpha(30),
                        alignment: Alignment.center,
                        child: const SizedBox(
                          height: 48,
                          width: 48,
                          child: CircularProgressIndicator(strokeWidth: 3),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          SafeArea(
            top: false,
            child: Container(
              padding: const EdgeInsets.only(
                  left: 24, right: 24, bottom: 16, top: 12),
              decoration: BoxDecoration(
                color: isDark ? DmColors.surfaceDark : DmColors.surfaceLight,
                border: Border(top: BorderSide(color: borderColor)),
                boxShadow: const [
                  BoxShadow(
                      color: Color(0x14000000),
                      blurRadius: 16,
                      offset: Offset(0, -6))
                ],
              ),
              child: Row(
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        t(context, 'Total', 'Total'),
                        style: TextStyle(
                          color: mutedText,
                          fontSize: 12,
                        ),
                      ),
                      Text(
                        '$_currency ${total.toStringAsFixed(2)}',
                        style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                            color: DmColors.doorOrange),
                      ),
                    ],
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: DmPrimaryButton(
                      label: t(context, 'Passer la commande', 'Checkout'),
                      onPressed:
                          (_loading || _submittingOrder) ? null : _placeOrder,
                      height: 52,
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

  Widget _summaryRow(String label, String value, {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          Text(value,
              style: TextStyle(
                  fontWeight: isBold ? FontWeight.w700 : FontWeight.w600)),
        ],
      ),
    );
  }

  Widget _iconBadge(IconData icon) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 36,
      width: 36,
      decoration: BoxDecoration(
        color: DmColors.iconBg(isDark),
        borderRadius: DmRadius.r12,
        border: Border.all(color: DmColors.border(isDark)),
      ),
      child: Icon(icon, color: DmColors.iconFg(isDark), size: 16),
    );
  }

  Widget _stepRow() {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final t = _tr;
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          _stepChip(t(context, 'Adresse', 'Address'), DmColors.doorBlue),
          _stepChip(t(context, 'Paiement', 'Payment'), DmColors.doorOrange),
          _stepChip(
            t(context, 'Confirmation', 'Confirmation'),
            DmColors.iconBg(isDark),
            textColor: DmColors.iconFg(isDark),
          ),
        ],
      ),
    );
  }

  Widget _stepChip(String label, Color bg, {Color textColor = Colors.white}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: DmRadius.r12),
      child: Text(label,
          style: TextStyle(
              fontSize: 12, fontWeight: FontWeight.w600, color: textColor)),
    );
  }

  Widget _paymentOptionCard({
    required String label,
    required String hint,
    required bool selected,
    required VoidCallback onTap,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = selected
        ? DmColors.doorBlue
        : (isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight);
    final textColor = selected
        ? Colors.white
        : (isDark ? DmColors.textPrimaryDark : DmColors.textPrimaryLight);
    final border = selected
        ? DmColors.doorBlue
        : (isDark ? DmColors.borderDark : DmColors.borderLight);
    return Semantics(
      button: true,
      selected: selected,
      label: '$label. $hint',
      child: InkWell(
        borderRadius: DmRadius.r12,
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: bg,
            borderRadius: DmRadius.r12,
            border: Border.all(color: border),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label,
                  style:
                      TextStyle(fontWeight: FontWeight.w600, color: textColor)),
              const SizedBox(height: 4),
              Text(hint,
                  style:
                      TextStyle(fontSize: 12, color: textColor.withAlpha(217))),
            ],
          ),
        ),
      ),
    );
  }

  Widget _prepaidPreview() {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final t = _tr;
    final mutedText = DmColors.mutedText(isDark);

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: DmColors.altSurface(isDark),
        borderRadius: DmRadius.r14,
        border: Border.all(color: DmColors.border(isDark)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                t(context, 'Carte prepayee', 'Prepaid card'),
                style: TextStyle(fontSize: 12, color: mutedText),
              ),
              const Text('DoorMarket',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
            ],
          ),
          const SizedBox(height: 10),
          const Text('4111 1111 1111 1111',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                t(context, 'Client', 'Customer'),
                style: TextStyle(fontSize: 12, color: mutedText),
              ),
              Text('12/28', style: TextStyle(fontSize: 12, color: mutedText)),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _applyPromo({bool showSnack = true}) async {
    final code = (_promoController.text).trim();
    if (code.isEmpty || _cart == null) {
      setState(() {
        _promoCode = null;
        _promoApplied = false;
        _discount = 0;
        _promoMessage = null;
      });
      return;
    }

    setState(() {
      _promoApplying = true;
      _promoCode = code;
    });

    try {
      final validation = await ref.read(cartApiProvider).validateCoupon(
            CouponValidationRequest(
              code: code,
              deliveryZoneId: _selected?.deliveryZoneId,
              requireDeliveryZone: true,
            ),
          );

      final warningMessage = validation.warnings.isNotEmpty
          ? validation.warnings.first.message
          : null;
      final blockingMessage = validation.blockingIssues.isNotEmpty
          ? validation.blockingIssues.first.message
          : null;
      final feedback = warningMessage ?? blockingMessage ?? validation.message;

      setState(() {
        _promoApplied = validation.applied;
        _promoCode = validation.applied ? (validation.promoCode ?? code) : code;
        _discount = validation.applied ? validation.discount : 0;
        _promoMessage = feedback;
      });
      if (validation.applied && _promoCode != null) {
        _promoController.value = TextEditingValue(
          text: _promoCode!,
          selection: TextSelection.collapsed(offset: _promoCode!.length),
        );
      }

      if (showSnack && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(feedback)),
        );
      }
    } catch (e) {
      if (!mounted) {
        return;
      }
      final message = UserErrorService.message(
        context,
        e,
        fallbackFr: 'Validation du coupon indisponible.',
        fallbackEn: 'Coupon validation is unavailable.',
      );
      setState(() {
        _promoApplied = false;
        _discount = 0;
        _promoMessage = message;
      });
      if (showSnack && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _promoApplying = false);
      }
    }
  }

  Future<void> _placeOrder() async {
    if (_loading || _submittingOrder) return;
    if (!_validateCheckout()) return;
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    setState(() {
      _loading = true;
      _submittingOrder = true;
    });

    try {
      await _trackCheckoutEvent(
        eventName: 'checkout_submit_clicked',
        paymentProvider: _payment,
        paymentChannel: _resolvePaymentChannel(_payment),
        success: true,
      );

      final line1 = _selected!.district.isEmpty
          ? _selected!.street
          : '${_selected!.street}, ${_selected!.district}';
      final request = CheckoutRequest(
        deliveryName: _selected!.fullName,
        deliveryPhone: _selected!.phone,
        deliveryLine1: line1,
        deliveryCity: _selected!.city,
        deliveryCountry: _selected!.country,
        deliveryZoneId: _selected!.deliveryZoneId,
        deliveryNotes: _selected!.landmark,
        promoCode: _promoApplied ? _promoCode : null,
        paymentProvider: _payment,
        prepaidCardCode: _payment == 'PrepaidCard' ? _prepaidCode : null,
      );

      final createdOrder = await ref.read(ordersApiProvider).create(request);
      await _trackCheckoutEvent(
        eventName: 'checkout_order_created',
        orderId: createdOrder.id,
        paymentProvider: _payment,
        paymentChannel: _resolvePaymentChannel(_payment),
        success: true,
      );
      try {
        await ref.read(cartApiProvider).clear();
      } catch (_) {}

      String? paymentWarning;
      var paymentRouteProvider = _payment;
      try {
        final paymentsApi = ref.read(paymentsApiProvider);
        if (_payment == 'Stripe') {
          final externalResult =
              await CheckoutHardeningService.openExternalCheckoutWithFallback(
            preferredProvider: _payment,
            createCheckoutUrl: (provider) async {
              if (provider == 'Stripe') {
                final checkout =
                    await paymentsApi.createStripeCheckout(createdOrder.id);
                return checkout.url;
              }
              final checkout =
                  await paymentsApi.createPayPalCheckout(createdOrder.id);
              return checkout.url;
            },
          );
          if (externalResult.providerUsed != null) {
            paymentRouteProvider = externalResult.providerUsed!;
          }
          if (!externalResult.launched) {
            await _trackCheckoutEvent(
              eventName: 'checkout_payment_redirect_failed',
              orderId: createdOrder.id,
              paymentProvider: paymentRouteProvider,
              paymentChannel: 'External',
              success: false,
              errorCode: 'external_provider_launch_failed',
              errorMessage: CheckoutHardeningService.summarizeAttempts(
                  externalResult.attempts),
            );
            paymentWarning = _trByLang(
              isFr,
              'Commande creee. Impossible d\'ouvrir Stripe/PayPal, finalisez le paiement depuis le detail commande. ${CheckoutHardeningService.summarizeAttempts(externalResult.attempts)}',
              'Order created. Unable to open Stripe/PayPal, finish payment from order details. ${CheckoutHardeningService.summarizeAttempts(externalResult.attempts)}',
            );
          } else {
            await _trackCheckoutEvent(
              eventName: 'checkout_payment_redirect_opened',
              orderId: createdOrder.id,
              paymentProvider: paymentRouteProvider,
              paymentChannel: 'External',
              success: true,
            );
            if (externalResult.usedFallback) {
              paymentWarning = _trByLang(
                isFr,
                'Commande creee. Bascule automatique vers ${externalResult.providerUsed} pour finaliser le paiement.',
                'Order created. Automatic fallback to ${externalResult.providerUsed} to complete payment.',
              );
            }
          }
        } else if (_payment == 'PayPal') {
          final externalResult =
              await CheckoutHardeningService.openExternalCheckoutWithFallback(
            preferredProvider: _payment,
            createCheckoutUrl: (provider) async {
              if (provider == 'PayPal') {
                final checkout =
                    await paymentsApi.createPayPalCheckout(createdOrder.id);
                return checkout.url;
              }
              final checkout =
                  await paymentsApi.createStripeCheckout(createdOrder.id);
              return checkout.url;
            },
          );
          if (externalResult.providerUsed != null) {
            paymentRouteProvider = externalResult.providerUsed!;
          }
          if (!externalResult.launched) {
            await _trackCheckoutEvent(
              eventName: 'checkout_payment_redirect_failed',
              orderId: createdOrder.id,
              paymentProvider: paymentRouteProvider,
              paymentChannel: 'External',
              success: false,
              errorCode: 'external_provider_launch_failed',
              errorMessage: CheckoutHardeningService.summarizeAttempts(
                  externalResult.attempts),
            );
            paymentWarning = _trByLang(
              isFr,
              'Commande creee. Impossible d\'ouvrir PayPal/Stripe, finalisez le paiement depuis le detail commande. ${CheckoutHardeningService.summarizeAttempts(externalResult.attempts)}',
              'Order created. Unable to open PayPal/Stripe, finish payment from order details. ${CheckoutHardeningService.summarizeAttempts(externalResult.attempts)}',
            );
          } else {
            await _trackCheckoutEvent(
              eventName: 'checkout_payment_redirect_opened',
              orderId: createdOrder.id,
              paymentProvider: paymentRouteProvider,
              paymentChannel: 'External',
              success: true,
            );
            if (externalResult.usedFallback) {
              paymentWarning = _trByLang(
                isFr,
                'Commande creee. Bascule automatique vers ${externalResult.providerUsed} pour finaliser le paiement.',
                'Order created. Automatic fallback to ${externalResult.providerUsed} to complete payment.',
              );
            }
          }
        } else if (_payment == 'MobileMoney') {
          final normalizedPhone = _normalizePhone(_mobileMoneyPhone);
          final mobileMoneyResult = await _initiateMobileMoneyWithFallback(
            orderId: createdOrder.id,
            phoneNumber: normalizedPhone,
          );
          final initiation = mobileMoneyResult.initiation;
          if (initiation == null) {
            await _trackCheckoutEvent(
              eventName: 'checkout_payment_initiation_failed',
              orderId: createdOrder.id,
              paymentProvider: _mobileMoneyProvider,
              paymentChannel: 'MobileMoney',
              success: false,
              errorCode: 'mobile_money_initiate_failed',
              errorMessage: CheckoutHardeningService.summarizeAttempts(
                  mobileMoneyResult.attempts),
            );
            paymentWarning = _trByLang(
              isFr,
              'Commande creee mais initiation Mobile Money indisponible. ${CheckoutHardeningService.summarizeAttempts(mobileMoneyResult.attempts)}',
              'Order created but Mobile Money initiation is unavailable. ${CheckoutHardeningService.summarizeAttempts(mobileMoneyResult.attempts)}',
            );
          } else {
            if (mobileMoneyResult.providerUsed != null) {
              _mobileMoneyProvider = mobileMoneyResult.providerUsed!;
            }
            if (mobileMoneyResult.usedFallback) {
              paymentWarning = _trByLang(
                isFr,
                'Commande creee. Mobile Money bascule vers ${mobileMoneyResult.providerUsed}.',
                'Order created. Mobile Money fallback to ${mobileMoneyResult.providerUsed}.',
              );
            }

            if (initiation.checkoutUrl != null &&
                initiation.checkoutUrl!.trim().isNotEmpty) {
              final opened = await launchUrl(
                Uri.parse(initiation.checkoutUrl!),
                mode: LaunchMode.externalApplication,
              );
              if (!opened) {
                await _trackCheckoutEvent(
                  eventName: 'checkout_payment_redirect_failed',
                  orderId: createdOrder.id,
                  paymentProvider: _mobileMoneyProvider,
                  paymentChannel: 'MobileMoney',
                  success: false,
                  errorCode: 'mobile_money_checkout_open_failed',
                );
                paymentWarning = _trByLang(
                  isFr,
                  'Commande creee. Paiement Mobile Money initie mais URL non ouverte. Confirmez depuis le detail commande.',
                  'Order created. Mobile Money initiated but URL could not open. Confirm from order details.',
                );
              } else {
                await _trackCheckoutEvent(
                  eventName: 'checkout_payment_redirect_opened',
                  orderId: createdOrder.id,
                  paymentProvider: _mobileMoneyProvider,
                  paymentChannel: 'MobileMoney',
                  success: true,
                  metadata: {
                    'transactionId': initiation.transactionId,
                  },
                );
              }
            } else {
              final status = await paymentsApi.confirmMobileMoney(
                orderId: createdOrder.id,
                provider: _mobileMoneyProvider,
                transactionId: initiation.transactionId,
              );
              await _trackCheckoutEvent(
                eventName: status.paid
                    ? 'checkout_payment_confirmed'
                    : 'checkout_payment_failed',
                orderId: createdOrder.id,
                paymentProvider: _mobileMoneyProvider,
                paymentChannel: 'MobileMoney',
                success: status.paid,
                errorCode: status.paid ? null : status.paymentStatus,
                errorMessage: status.paid ? null : status.message,
              );
              if (!status.paid) {
                paymentWarning = status.message ??
                    _trByLang(
                      isFr,
                      'Paiement Mobile Money en attente. Confirmez depuis le detail commande.',
                      'Mobile Money payment pending. Confirm from order details.',
                    );
              }
            }
          }
        } else if (_payment == 'PrepaidCard') {
          await paymentsApi.payWithPrepaidCard(createdOrder.id, _prepaidCode);
          await _trackCheckoutEvent(
            eventName: 'checkout_payment_confirmed',
            orderId: createdOrder.id,
            paymentProvider: 'PrepaidCard',
            paymentChannel: 'Prepaid',
            success: true,
          );
        }
      } catch (e) {
        final friendly = UserErrorService.messageByLanguage(
          isFr: isFr,
          error: e,
          fallbackFr: 'Paiement non finalise.',
          fallbackEn: 'Payment not completed.',
        );
        await _trackCheckoutEvent(
          eventName: 'checkout_payment_failed',
          orderId: createdOrder.id,
          paymentProvider: _payment,
          paymentChannel: _resolvePaymentChannel(_payment),
          success: false,
          errorCode: 'post_order_payment_exception',
          errorMessage: e.toString(),
        );
        paymentWarning = _trByLang(
          isFr,
          'Commande creee mais paiement non finalise. Ouvrez le detail commande pour reprendre le paiement. ($friendly)',
          'Order created but payment is not completed. Open order details to resume payment. ($friendly)',
        );
      }

      ref.invalidate(ordersProvider);
      ref.invalidate(orderDetailsProvider(createdOrder.id));

      if (!mounted) return;
      final needsPaymentReturn =
          paymentRouteProvider == 'Stripe' || paymentRouteProvider == 'PayPal';
      if (needsPaymentReturn) {
        final route = Uri(
          path: '/payment-return/${createdOrder.id}',
          queryParameters: {
            'provider': paymentRouteProvider,
            if (paymentWarning != null && paymentWarning.isNotEmpty)
              'notice': paymentWarning,
          },
        ).toString();
        context.go(route);
      } else {
        if (paymentWarning != null && paymentWarning.isNotEmpty) {
          _notify(paymentWarning);
        }
        context.go('/orders/${createdOrder.id}');
      }
    } catch (e) {
      await _trackCheckoutEvent(
        eventName: 'checkout_payment_failed',
        paymentProvider: _payment,
        paymentChannel: _resolvePaymentChannel(_payment),
        success: false,
        errorCode: 'checkout_order_exception',
        errorMessage: e.toString(),
      );
      setState(() {
        _error = UserErrorService.message(
          context,
          e,
          fallbackFr: 'La commande n a pas pu etre creee.',
          fallbackEn: 'The order could not be created.',
        );
      });
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
          _submittingOrder = false;
        });
      }
    }
  }

  Future<_MobileMoneyInitResult> _initiateMobileMoneyWithFallback({
    required String orderId,
    required String phoneNumber,
  }) async {
    final attempts = <CheckoutAttemptLog>[];
    final api = ref.read(paymentsApiProvider);
    final orderedProviders =
        CheckoutHardeningService.mobileMoneyFallbackOrder(_mobileMoneyProvider);

    for (final provider in orderedProviders) {
      try {
        final initiation = await api.initiateMobileMoney(
          orderId: orderId,
          provider: provider,
          phoneNumber: phoneNumber,
          callbackUrl: null,
        );
        attempts.add(CheckoutAttemptLog(
          provider: provider,
          stage: 'initiate',
          detail: 'Started.',
        ));
        return _MobileMoneyInitResult(
          initiation: initiation,
          providerUsed: provider,
          attempts: attempts,
          usedFallback: provider != orderedProviders.first,
        );
      } catch (error) {
        attempts.add(CheckoutAttemptLog(
          provider: provider,
          stage: 'exception',
          detail: error.toString(),
        ));
      }
    }

    return _MobileMoneyInitResult(
      initiation: null,
      providerUsed: null,
      attempts: attempts,
      usedFallback: false,
    );
  }

  void _onPaymentMethodSelected(String method) {
    if (_payment == method) {
      return;
    }

    setState(() => _payment = method);
    _trackCheckoutEvent(
      eventName: 'checkout_payment_method_selected',
      paymentProvider: method,
      paymentChannel: _resolvePaymentChannel(method),
      success: true,
    );
  }

  Future<void> _trackCheckoutEvent({
    required String eventName,
    String? orderId,
    String? paymentProvider,
    String? paymentChannel,
    bool? success,
    int? durationMs,
    String? errorCode,
    String? errorMessage,
    Map<String, dynamic>? metadata,
  }) async {
    await ref.read(checkoutObservabilityServiceProvider).track(
          eventName: eventName,
          orderId: orderId,
          paymentProvider: paymentProvider,
          paymentChannel: paymentChannel,
          success: success,
          durationMs: durationMs,
          errorCode: errorCode,
          errorMessage: errorMessage,
          metadata: metadata,
        );
  }

  String _resolvePaymentChannel(String provider) {
    final normalized = provider.trim().toLowerCase();
    if (normalized == 'stripe' || normalized == 'paypal') {
      return 'External';
    }
    if (normalized == 'mobilemoney') {
      return 'MobileMoney';
    }
    if (normalized == 'prepaidcard' || normalized == 'prepaidinternal') {
      return 'Prepaid';
    }
    return provider;
  }

  bool _validateCheckout() {
    if (_cart == null) {
      _notify(_tr(context, 'Votre panier est vide.', 'Your cart is empty.'));
      return false;
    }
    if (_selected == null) {
      _notify(_tr(context, 'Veuillez choisir une adresse de livraison.',
          'Please choose a delivery address.'));
      return false;
    }
    if (_selected!.deliveryZoneId == null) {
      _notify(_tr(context, 'Zone de livraison requise pour cette adresse.',
          'A delivery zone is required for this address.'));
      return false;
    }
    if (_payment == 'PrepaidCard' && _prepaidCode.trim().isEmpty) {
      _notify(_tr(context, 'Veuillez saisir le code de la carte prepayee.',
          'Please enter the prepaid card code.'));
      return false;
    }
    if (_payment == 'MobileMoney' &&
        _normalizePhone(_mobileMoneyPhone).isEmpty) {
      _notify(_tr(context, 'Veuillez saisir un numero Mobile Money.',
          'Please enter a Mobile Money number.'));
      return false;
    }
    return true;
  }

  void _notify(String message) {
    setState(() => _error = message);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
    _speak(message);
  }

  Future<void> _speak(String message) async {
    try {
      final locale = Localizations.localeOf(context);
      final lang =
          locale.languageCode.toLowerCase() == 'fr' ? 'fr-FR' : 'en-US';
      await _tts.setLanguage(lang);
      await _tts.setSpeechRate(0.45);
      await _tts.setPitch(1.0);
      await _tts.stop();
      await _tts.speak(message);
    } catch (_) {}
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }

  static String _trByLang(bool isFr, String fr, String en) {
    return isFr ? fr : en;
  }

  static String _normalizePhone(String value) {
    return value
        .trim()
        .replaceAll(' ', '')
        .replaceAll('-', '')
        .replaceAll('(', '')
        .replaceAll(')', '');
  }

  @override
  bool get wantKeepAlive => true;
}

class _MobileMoneyInitResult {
  final MobileMoneyInitiateResult? initiation;
  final String? providerUsed;
  final List<CheckoutAttemptLog> attempts;
  final bool usedFallback;

  const _MobileMoneyInitResult({
    required this.initiation,
    required this.providerUsed,
    required this.attempts,
    required this.usedFallback,
  });
}
