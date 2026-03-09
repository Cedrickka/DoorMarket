import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';

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
import 'orders_provider.dart';

class OrderDetailsScreen extends ConsumerStatefulWidget {
  final String orderId;

  const OrderDetailsScreen({super.key, required this.orderId});

  @override
  ConsumerState<OrderDetailsScreen> createState() => _OrderDetailsScreenState();
}

class _OrderDetailsScreenState extends ConsumerState<OrderDetailsScreen>
    with WidgetsBindingObserver {
  final _prepaidController = TextEditingController();
  final _mobileMoneyController = TextEditingController();
  String? _mobileMoneyTransactionId;
  String? _mobileMoneyProviderUsed;
  bool _paymentActionBusy = false;
  String? _paymentHint;
  bool _paymentHintIsError = false;
  bool _autoRefreshEnabled = false;
  Timer? _autoRefreshTimer;
  DateTime? _lastAutoRefreshAt;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _autoRefreshTimer = Timer.periodic(
      const Duration(seconds: 12),
      (_) => _autoRefreshTick(),
    );
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _autoRefreshEnabled) {
      _autoRefreshTick();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _autoRefreshTimer?.cancel();
    _prepaidController.dispose();
    _mobileMoneyController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final data = ref.watch(orderDetailsProvider(widget.orderId));

    return Scaffold(
      body: data.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => _detailsErrorState(context, err),
        data: (order) => _content(context, ref, order),
      ),
    );
  }

  Widget _detailsErrorState(BuildContext context, Object error) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final message = UserErrorService.message(
      context,
      error,
      fallbackFr: 'Le detail de la commande est indisponible.',
      fallbackEn: 'Order details are currently unavailable.',
    );
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: DmCard(
          margin: EdgeInsets.zero,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.error_outline,
                color: isDark ? DmColors.errorDark : DmColors.errorLight,
              ),
              const SizedBox(height: 8),
              Text(
                message,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: 12),
              DmSecondaryButton(
                label: _tr(context, 'Reessayer', 'Retry'),
                height: 42,
                onPressed: () => _refreshOrder(ref),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _content(BuildContext context, WidgetRef ref, OrderDto order) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final status = order.fulfillmentStatus.isNotEmpty
        ? order.fulfillmentStatus
        : order.status;
    final paymentStatus = order.paymentStatus.trim().toLowerCase();
    final isPaid = paymentStatus == 'paid';
    _autoRefreshEnabled = !isPaid;
    final paymentPending = paymentStatus == 'pending';
    final paymentFailed = paymentStatus == 'failed';
    final provider = _resolvePaymentProvider(order);
    final dateLabel =
        DateFormat('dd/MM/yyyy').format(order.createdAtUtc.toLocal());
    final statusLower = status.toLowerCase();
    final preparing = statusLower.contains('processing') ||
        statusLower.contains('prepar') ||
        statusLower.contains('ready') ||
        statusLower.contains('delivered');
    final ready =
        statusLower.contains('ready') || statusLower.contains('delivered');
    final delivered = statusLower.contains('delivered');
    final canRequestReturn = _canRequestReturn(order, statusLower);

    return Column(
      children: [
        DmHeader(
          title: _tr(context, 'Detail commande', 'Order details'),
          subtitle: _tr(context, 'Statut et paiement', 'Status and payment'),
          showBack: true,
          onBack: () => Navigator.of(context).maybePop(),
        ),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              DmCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(order.id.substring(0, 6),
                                  style: const TextStyle(
                                      fontSize: 20,
                                      fontWeight: FontWeight.w700)),
                              const SizedBox(height: 4),
                              Text(dateLabel,
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: DmColors.mutedText(isDark),
                                  )),
                            ],
                          ),
                        ),
                        _statusBadge(status),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          _tr(context, 'Paiement', 'Payment'),
                          style: TextStyle(
                            fontSize: 12,
                            color: DmColors.mutedText(isDark),
                          ),
                        ),
                        _paymentBadge(
                            order.paymentStatus, order.paymentProvider),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          _tr(context, 'Articles', 'Items'),
                          style: TextStyle(
                            fontSize: 12,
                            color: DmColors.mutedText(isDark),
                          ),
                        ),
                        Text('${order.items.length}',
                            style:
                                const TextStyle(fontWeight: FontWeight.w600)),
                      ],
                    ),
                  ],
                ),
              ),
              DmCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(_tr(context, 'Progression', 'Progress'),
                        style: Theme.of(context).textTheme.headlineSmall),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                            child: _stageChip(
                                _tr(context, 'Créé', 'Created'), true)),
                        const SizedBox(width: 6),
                        Expanded(
                            child: _stageChip(
                                _tr(context, 'Préparé', 'Prepared'),
                                preparing)),
                        const SizedBox(width: 6),
                        Expanded(
                            child: _stageChip(
                                _tr(context, 'Prêt', 'Ready'), ready)),
                        const SizedBox(width: 6),
                        Expanded(
                            child: _stageChip(
                                _tr(context, 'Livré', 'Delivered'), delivered)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${_tr(context, 'Statut', 'Status')}: $status',
                      style: TextStyle(color: DmColors.mutedText(isDark)),
                    ),
                  ],
                ),
              ),
              if (canRequestReturn)
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _tr(context, 'Retour / remboursement',
                            'Return / refund'),
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _tr(
                          context,
                          'Cette commande est eligible au retour.',
                          'This order is eligible for return.',
                        ),
                        style: TextStyle(color: DmColors.mutedText(isDark)),
                      ),
                      const SizedBox(height: 10),
                      DmSecondaryButton(
                        label: _tr(
                            context, 'Demander un retour', 'Request return'),
                        height: 42,
                        onPressed: () =>
                            context.push('/returns?orderId=${order.id}'),
                      ),
                    ],
                  ),
                ),
              if (!isPaid)
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        paymentPending
                            ? _tr(context, 'Paiement en attente',
                                'Payment pending')
                            : paymentFailed
                                ? _tr(context, 'Paiement a reprendre',
                                    'Payment retry required')
                                : _tr(context, 'Finaliser le paiement',
                                    'Complete payment'),
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        paymentPending
                            ? _tr(
                                context,
                                'Le paiement est en cours ou interrompu. Reprenez-le puis actualisez le statut.',
                                'Payment is in progress or was interrupted. Resume it and refresh the status.',
                              )
                            : paymentFailed
                                ? _tr(
                                    context,
                                    'Le paiement precedent a echoue. Choisissez une action pour retenter.',
                                    'The previous payment failed. Choose an action to retry.',
                                  )
                                : _tr(
                                    context,
                                    'Choisissez un mode de paiement.',
                                    'Choose a payment method.'),
                        style: TextStyle(color: DmColors.mutedText(isDark)),
                      ),
                      const SizedBox(height: 12),
                      if (provider != 'prepaid')
                        Row(
                          children: [
                            Expanded(
                              child: DmSecondaryButton(
                                label: provider == 'stripe'
                                    ? 'Stripe'
                                    : provider == 'mobilemoney'
                                        ? _tr(context, 'Mobile Money',
                                            'Mobile Money')
                                        : 'PayPal',
                                height: 44,
                                onPressed: _paymentActionBusy
                                    ? null
                                    : () => _runPaymentAction(
                                          context,
                                          () {
                                            if (provider == 'stripe') {
                                              return _payWithStripe(
                                                  ref, context, order);
                                            }
                                            if (provider == 'mobilemoney') {
                                              return _payWithMobileMoney(
                                                  ref, context, order);
                                            }
                                            return _payWithPayPal(
                                                ref, context, order);
                                          },
                                          successMessage:
                                              provider == 'mobilemoney'
                                                  ? _tr(
                                                      context,
                                                      'Demande Mobile Money envoyee. Actualisez le statut ou confirmez.',
                                                      'Mobile Money request sent. Refresh status or confirm.',
                                                    )
                                                  : _tr(
                                                      context,
                                                      'Canal de paiement ouvert. Revenez ici puis actualisez le statut.',
                                                      'Payment channel opened. Come back here and refresh status.',
                                                    ),
                                        ),
                              ),
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: DmPrimaryButton(
                                label: _tr(context, 'Prepayee', 'Prepaid'),
                                height: 44,
                                onPressed: _paymentActionBusy
                                    ? null
                                    : () => _runPaymentAction(
                                          context,
                                          () => _payWithPrepaid(
                                              ref, context, order),
                                          successMessage: _tr(
                                            context,
                                            'Paiement prepayee envoye. Statut actualise.',
                                            'Prepaid payment sent. Status refreshed.',
                                          ),
                                        ),
                              ),
                            ),
                          ],
                        )
                      else
                        DmPrimaryButton(
                          label: _tr(
                              context, 'Payer en prepayee', 'Pay with prepaid'),
                          height: 44,
                          onPressed: _paymentActionBusy
                              ? null
                              : () => _runPaymentAction(
                                    context,
                                    () => _payWithPrepaid(ref, context, order),
                                    successMessage: _tr(
                                      context,
                                      'Paiement prepayee envoye. Statut actualise.',
                                      'Prepaid payment sent. Status refreshed.',
                                    ),
                                  ),
                        ),
                      if (provider == 'mobilemoney') ...[
                        const SizedBox(height: 10),
                        TextField(
                          controller: _mobileMoneyController,
                          decoration: InputDecoration(
                            labelText: _tr(context, 'Numero Mobile Money',
                                'Mobile Money number'),
                            hintText: '+243...',
                          ),
                          enabled: !_paymentActionBusy,
                        ),
                        const SizedBox(height: 8),
                        DmSecondaryButton(
                          label: _tr(context, 'Confirmer Mobile Money',
                              'Confirm Mobile Money'),
                          height: 42,
                          onPressed: _paymentActionBusy
                              ? null
                              : () => _runPaymentAction(
                                    context,
                                    () => _confirmMobileMoney(
                                        ref, context, order),
                                    successMessage: _tr(
                                      context,
                                      'Confirmation Mobile Money executee.',
                                      'Mobile Money confirmation executed.',
                                    ),
                                  ),
                        ),
                      ],
                      const SizedBox(height: 10),
                      DmSecondaryButton(
                        label: _tr(
                            context, 'Rafraichir le statut', 'Refresh status'),
                        height: 42,
                        onPressed: _paymentActionBusy
                            ? null
                            : () => _runPaymentAction(
                                  context,
                                  () => _refreshOrder(ref),
                                  successMessage: _tr(context,
                                      'Statut actualise.', 'Status updated.'),
                                ),
                      ),
                      const SizedBox(height: 10),
                      DmSecondaryButton(
                        label: _tr(context, 'Besoin d\'aide support',
                            'Need support help'),
                        height: 42,
                        onPressed: _paymentActionBusy
                            ? null
                            : () => _openSupportWithPrefill(context, order),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _prepaidController,
                        decoration: InputDecoration(
                            labelText: _tr(context, 'Numero carte prepayee',
                                'Prepaid card number')),
                        enabled: !_paymentActionBusy,
                      ),
                      if (_paymentActionBusy) ...[
                        const SizedBox(height: 10),
                        const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      ],
                      if (_paymentHint != null && _paymentHint!.isNotEmpty) ...[
                        const SizedBox(height: 10),
                        Text(
                          _paymentHint!,
                          style: TextStyle(
                            fontSize: 12,
                            color: _paymentHintIsError
                                ? (isDark
                                    ? DmColors.errorDark
                                    : DmColors.errorLight)
                                : (isDark
                                    ? DmColors.successDark
                                    : DmColors.successLight),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                      const SizedBox(height: 8),
                      Text(
                        _lastAutoRefreshAt == null
                            ? _tr(
                                context,
                                'Actualisation automatique du statut toutes les 12 secondes.',
                                'Status auto-refresh runs every 12 seconds.',
                              )
                            : _tr(
                                context,
                                'Derniere actualisation auto: ${DateFormat('HH:mm:ss').format(_lastAutoRefreshAt!)}',
                                'Last auto-refresh: ${DateFormat('HH:mm:ss').format(_lastAutoRefreshAt!)}',
                              ),
                        style: TextStyle(
                          fontSize: 12,
                          color: DmColors.mutedText(isDark),
                        ),
                      ),
                    ],
                  ),
                ),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(_tr(context, 'Articles', 'Items'),
                      style: Theme.of(context).textTheme.headlineSmall),
                  Text(
                    '${order.items.length} ${_tr(context, 'article(s)', 'item(s)')}',
                    style: TextStyle(
                      fontSize: 12,
                      color: DmColors.mutedText(isDark),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              ...order.items.map(
                (item) => DmCard(
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(item.productName,
                                style: const TextStyle(
                                    fontWeight: FontWeight.w600)),
                            const SizedBox(height: 6),
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 8, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: DmColors.iconBg(isDark),
                                    borderRadius: DmRadius.r12,
                                  ),
                                  child: Text('x${item.qty}',
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: isDark
                                            ? DmColors.textPrimaryDark
                                            : DmColors.doorBlue,
                                      )),
                                ),
                                const SizedBox(width: 8),
                                Text(
                                  '${item.unitPrice.toStringAsFixed(2)} / ${_tr(context, 'u', 'unit')}',
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: DmColors.mutedText(isDark),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(item.lineTotal.toStringAsFixed(2),
                              style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                  color: DmColors.doorOrange)),
                          Text(item.currency,
                              style: const TextStyle(
                                  fontSize: 12, color: DmColors.doorOrange)),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              DmCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(_tr(context, 'Montants', 'Amounts'),
                        style: Theme.of(context).textTheme.headlineSmall),
                    const SizedBox(height: 12),
                    _summaryRow(_tr(context, 'Sous-total', 'Subtotal'),
                        '${order.currency} ${order.subtotal.toStringAsFixed(2)}'),
                    _summaryRow(_tr(context, 'Livraison', 'Delivery'),
                        '${order.currency} ${order.deliveryFee.toStringAsFixed(2)}'),
                    _summaryRow(_tr(context, 'Remise', 'Discount'),
                        '${order.currency} ${order.discount.toStringAsFixed(2)}'),
                    const Divider(),
                    _summaryRow(_tr(context, 'Total', 'Total'),
                        '${order.currency} ${order.totalAmount.toStringAsFixed(2)}',
                        isBold: true),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
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
                  fontWeight: isBold ? FontWeight.w700 : FontWeight.w500)),
        ],
      ),
    );
  }

  Widget _statusBadge(String status) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isDelivered = status.toLowerCase() == 'delivered';
    final bg =
        isDelivered ? DmColors.successSurface(isDark) : DmColors.iconBg(isDark);
    final border =
        isDelivered ? DmColors.successLight : DmColors.border(isDark);
    final textColor = isDelivered
        ? (isDark ? DmColors.successDark : DmColors.successLight)
        : (DmColors.iconFg(isDark));
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: DmRadius.r12,
        border: Border.all(color: border),
      ),
      child: Text(status,
          style: TextStyle(
              fontSize: 12, fontWeight: FontWeight.w600, color: textColor)),
    );
  }

  Widget _paymentBadge(String status, String provider) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isPaid = status.toLowerCase() == 'paid';
    final bg =
        isPaid ? DmColors.successSurface(isDark) : DmColors.iconBg(isDark);
    final border = isPaid ? DmColors.successLight : DmColors.border(isDark);
    final textColor = isPaid
        ? (isDark ? DmColors.successDark : DmColors.successLight)
        : (DmColors.iconFg(isDark));
    final providerText = provider.isNotEmpty ? ' - $provider' : '';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: DmRadius.r12,
        border: Border.all(color: border),
      ),
      child: Text(
        '$status$providerText',
        style: TextStyle(
            fontSize: 12, fontWeight: FontWeight.w600, color: textColor),
      ),
    );
  }

  Widget _stageChip(String label, bool active) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = active
        ? DmColors.doorBlue
        : (isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight);
    final textColor = active ? Colors.white : (DmColors.iconFg(isDark));
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: DmRadius.r12,
        border: Border.all(
            color: active ? DmColors.doorBlue : DmColors.border(isDark)),
      ),
      child: Text(label, style: TextStyle(fontSize: 12, color: textColor)),
    );
  }

  Future<void> _payWithPayPal(
      WidgetRef ref, BuildContext context, OrderDto order) async {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final api = ref.read(paymentsApiProvider);
    final result =
        await CheckoutHardeningService.openExternalCheckoutWithFallback(
      preferredProvider: 'PayPal',
      createCheckoutUrl: (provider) async {
        if (provider == 'PayPal') {
          return (await api.createPayPalCheckout(order.id)).url;
        }
        return (await api.createStripeCheckout(order.id)).url;
      },
    );
    if (!result.launched) {
      throw Exception(_trByLang(
        isFr,
        'Impossible d\'ouvrir PayPal/Stripe. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}',
        'Unable to open PayPal/Stripe. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}',
      ));
    }
  }

  Future<void> _payWithStripe(
      WidgetRef ref, BuildContext context, OrderDto order) async {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final api = ref.read(paymentsApiProvider);
    final result =
        await CheckoutHardeningService.openExternalCheckoutWithFallback(
      preferredProvider: 'Stripe',
      createCheckoutUrl: (provider) async {
        if (provider == 'Stripe') {
          return (await api.createStripeCheckout(order.id)).url;
        }
        return (await api.createPayPalCheckout(order.id)).url;
      },
    );
    if (!result.launched) {
      throw Exception(_trByLang(
        isFr,
        'Impossible d\'ouvrir Stripe/PayPal. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}',
        'Unable to open Stripe/PayPal. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}',
      ));
    }
  }

  Future<void> _payWithMobileMoney(
      WidgetRef ref, BuildContext context, OrderDto order) async {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final api = ref.read(paymentsApiProvider);
    final provider = _mobileMoneyProviderFromOrder(order);
    final phone = _normalizePhone(_mobileMoneyController.text);
    if (phone.isEmpty) {
      throw Exception(_trByLang(
        isFr,
        'Numero Mobile Money requis.',
        'Mobile Money number is required.',
      ));
    }

    final attempts = <CheckoutAttemptLog>[];
    MobileMoneyInitiateResult? initiation;
    String? providerUsed;
    final orderedProviders =
        CheckoutHardeningService.mobileMoneyFallbackOrder(provider);
    for (final candidate in orderedProviders) {
      try {
        final current = await api.initiateMobileMoney(
          orderId: order.id,
          provider: candidate,
          phoneNumber: phone,
        );
        attempts.add(CheckoutAttemptLog(
          provider: candidate,
          stage: 'initiate',
          detail: 'Started.',
        ));
        initiation = current;
        providerUsed = candidate;
        break;
      } catch (error) {
        attempts.add(CheckoutAttemptLog(
          provider: candidate,
          stage: 'exception',
          detail: error.toString(),
        ));
      }
    }
    if (initiation == null || providerUsed == null) {
      throw Exception(_trByLang(
        isFr,
        'Initiation Mobile Money indisponible. ${CheckoutHardeningService.summarizeAttempts(attempts)}',
        'Mobile Money initiation unavailable. ${CheckoutHardeningService.summarizeAttempts(attempts)}',
      ));
    }
    _mobileMoneyProviderUsed = providerUsed;
    _mobileMoneyTransactionId = initiation.transactionId;

    if (initiation.checkoutUrl != null &&
        initiation.checkoutUrl!.trim().isNotEmpty) {
      final opened = await launchUrl(
        Uri.parse(initiation.checkoutUrl!),
        mode: LaunchMode.externalApplication,
      );
      if (!opened) {
        throw Exception(_trByLang(
            isFr,
            'Impossible d\'ouvrir la page Mobile Money.',
            'Unable to open Mobile Money page.'));
      }
      return;
    }
  }

  Future<void> _confirmMobileMoney(
      WidgetRef ref, BuildContext context, OrderDto order) async {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final api = ref.read(paymentsApiProvider);
    final provider =
        _mobileMoneyProviderUsed ?? _mobileMoneyProviderFromOrder(order);
    final txId = _extractTransactionId(order);
    if (txId == null || txId.isEmpty) {
      throw Exception(_trByLang(
        isFr,
        'Transaction Mobile Money introuvable. Relancez le paiement.',
        'Mobile Money transaction not found. Start payment again.',
      ));
    }

    final result = await api.confirmMobileMoney(
      orderId: order.id,
      provider: provider,
      transactionId: txId,
    );

    if (!result.paid) {
      throw Exception(result.message ??
          _trByLang(
            isFr,
            'Paiement Mobile Money en attente.',
            'Mobile Money payment is still pending.',
          ));
    }

    await _refreshOrder(ref);
  }

  Future<void> _payWithPrepaid(
      WidgetRef ref, BuildContext context, OrderDto order) async {
    final api = ref.read(paymentsApiProvider);
    final existing = _prepaidController.text.trim();
    final code =
        existing.isNotEmpty ? existing : await _promptPrepaidCode(context);
    if (code == null || code.isEmpty) return;
    await api.payWithPrepaidCard(order.id, code);
    await _refreshOrder(ref);
  }

  Future<void> _refreshOrder(WidgetRef ref) async {
    ref.invalidate(orderDetailsProvider(widget.orderId));
    ref.invalidate(ordersProvider);
    await ref.read(orderDetailsProvider(widget.orderId).future);
  }

  Future<void> _autoRefreshTick() async {
    if (!mounted || !_autoRefreshEnabled || _paymentActionBusy) {
      return;
    }

    try {
      await _refreshOrder(ref);
      if (!mounted) {
        return;
      }
      setState(() => _lastAutoRefreshAt = DateTime.now());
    } catch (_) {
      // Ignore transient failures during passive polling.
    }
  }

  Future<void> _runPaymentAction(
    BuildContext context,
    Future<void> Function() action, {
    String? successMessage,
  }) async {
    if (_paymentActionBusy) return;
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';

    setState(() {
      _paymentActionBusy = true;
      _paymentHint = null;
      _paymentHintIsError = false;
    });

    try {
      await action();
      if (!mounted) return;
      setState(() {
        _paymentHint = successMessage ??
            _trByLang(isFr, 'Action de paiement terminee.',
                'Payment action completed.');
        _paymentHintIsError = false;
      });
      ref.invalidate(ordersProvider);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _paymentHint = _readableError(isFr, e);
        _paymentHintIsError = true;
      });
    } finally {
      if (mounted) {
        setState(() => _paymentActionBusy = false);
      }
    }
  }

  String _readableError(bool isFr, Object error) {
    return UserErrorService.message(
      context,
      error,
      fallbackFr: _trByLang(
        isFr,
        'Action de paiement indisponible.',
        'Payment action unavailable.',
      ),
      fallbackEn: _trByLang(
        isFr,
        'Action de paiement indisponible.',
        'Payment action unavailable.',
      ),
    );
  }

  String _resolvePaymentProvider(OrderDto order) {
    final explicit = order.paymentProvider.trim();
    final snapshot = order.paymentMethodSnapshot?.provider ?? '';
    final raw = explicit.isNotEmpty ? explicit : snapshot;
    final normalized = raw.toLowerCase().replaceAll(' ', '');
    if (normalized.contains('stripe')) {
      return 'stripe';
    }
    if (normalized.contains('prepaid') || normalized.contains('voucher')) {
      return 'prepaid';
    }
    if (normalized.contains('mobilemoney') ||
        normalized.contains('airtel') ||
        normalized.contains('orange') ||
        normalized.contains('mpesa') ||
        normalized.contains('m-pesa')) {
      return 'mobilemoney';
    }
    return 'paypal';
  }

  String _mobileMoneyProviderFromOrder(OrderDto order) {
    final raw = order.paymentProvider.trim().toUpperCase();
    if (raw.contains("AIRTEL")) {
      return 'AIRTEL';
    }
    if (raw.contains("ORANGE")) {
      return 'ORANGE';
    }
    if (raw.contains("MPESA") || raw.contains("M-PESA")) {
      return 'MPESA';
    }
    return 'AIRTEL';
  }

  String? _extractTransactionId(OrderDto order) {
    if (_mobileMoneyTransactionId != null &&
        _mobileMoneyTransactionId!.trim().isNotEmpty) {
      return _mobileMoneyTransactionId!.trim();
    }
    final snapshotId = order.paymentMethodSnapshot?.providerPaymentIntentId;
    if (snapshotId != null && snapshotId.trim().isNotEmpty) {
      return snapshotId.trim();
    }
    return null;
  }

  String _normalizePhone(String value) {
    return value
        .trim()
        .replaceAll(' ', '')
        .replaceAll('-', '')
        .replaceAll('(', '')
        .replaceAll(')', '');
  }

  bool _canRequestReturn(OrderDto order, String normalizedStatus) {
    final payment = order.paymentStatus.trim().toLowerCase();
    return payment == 'paid' &&
        (normalizedStatus == 'delivered' || normalizedStatus == 'completed');
  }

  void _openSupportWithPrefill(BuildContext context, OrderDto order) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final paymentProvider = _resolvePaymentProvider(order).toUpperCase();
    final paymentStatus = order.paymentStatus.trim().isEmpty
        ? (isFr ? 'INCONNU' : 'UNKNOWN')
        : order.paymentStatus.trim().toUpperCase();
    final orderCode = order.id.substring(0, 8).toUpperCase();
    final subject = isFr
        ? 'Incident paiement commande $orderCode'
        : 'Payment issue for order $orderCode';
    final message = isFr
        ? 'Bonjour, j ai besoin d aide pour reprendre un paiement.\nCommande: $orderCode\nStatut paiement: $paymentStatus\nFournisseur: $paymentProvider'
        : 'Hello, I need help to resume a payment.\nOrder: $orderCode\nPayment status: $paymentStatus\nProvider: $paymentProvider';

    final route = Uri(
      path: '/support',
      queryParameters: {
        'openForm': '1',
        'subject': subject,
        'message': message,
      },
    ).toString();
    context.push(route);
  }

  Future<String?> _promptPrepaidCode(BuildContext context) async {
    final controller = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(_tr(context, 'Code prepayee', 'Prepaid code')),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(
              border: OutlineInputBorder(), hintText: '4111 1111 1111 1111'),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: Text(_tr(context, 'Annuler', 'Cancel'))),
          TextButton(
              onPressed: () => Navigator.pop(ctx, controller.text.trim()),
              child: Text(_tr(context, 'Valider', 'Confirm'))),
        ],
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }

  static String _trByLang(bool isFr, String fr, String en) {
    return isFr ? fr : en;
  }
}
