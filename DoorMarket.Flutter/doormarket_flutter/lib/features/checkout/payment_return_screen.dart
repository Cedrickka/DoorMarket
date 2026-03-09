import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/models/orders.dart';
import '../../core/providers.dart';
import '../../core/services/checkout_hardening_service.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import '../orders/orders_provider.dart';

class PaymentReturnScreen extends ConsumerStatefulWidget {
  final String orderId;
  final String provider;
  final String? notice;

  const PaymentReturnScreen({
    super.key,
    required this.orderId,
    required this.provider,
    this.notice,
  });

  @override
  ConsumerState<PaymentReturnScreen> createState() =>
      _PaymentReturnScreenState();
}

class _PaymentReturnScreenState extends ConsumerState<PaymentReturnScreen>
    with WidgetsBindingObserver {
  OrderDto? _order;
  bool _loading = true;
  bool _reopeningProvider = false;
  String? _error;
  DateTime? _lastCheckAt;
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _refreshOrder(initial: true);
    _pollTimer = Timer.periodic(
      const Duration(seconds: 10),
      (_) => _pollIfNeeded(),
    );
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _pollIfNeeded();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _pollTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final provider = _providerLabel(widget.provider);
    final order = _order;
    final paymentStatus = (order?.paymentStatus ?? '').trim().toLowerCase();
    final isPaid = paymentStatus == 'paid';
    final isFailed = paymentStatus == 'failed';

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: _tr(
              isFr,
              'Retour paiement',
              'Payment return',
            ),
            subtitle: _tr(
              isFr,
              'Verification automatique du statut',
              'Automatic status verification',
            ),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: [
                if (widget.notice != null && widget.notice!.trim().isNotEmpty)
                  DmCard(
                    child: Text(
                      widget.notice!,
                      style: const TextStyle(
                        color: DmColors.doorOrange,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _tr(
                          isFr,
                          'Commande ${widget.orderId.substring(0, 8).toUpperCase()}',
                          'Order ${widget.orderId.substring(0, 8).toUpperCase()}',
                        ),
                        style: const TextStyle(
                            fontSize: 18, fontWeight: FontWeight.w700),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _tr(
                          isFr,
                          'Fournisseur: $provider',
                          'Provider: $provider',
                        ),
                        style: TextStyle(
                          fontSize: 12,
                          color: DmColors.mutedText(isDark),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _lastCheckAt == null
                            ? _tr(isFr, 'Verification en cours...',
                                'Checking status...')
                            : _tr(
                                isFr,
                                'Derniere verification: ${DateFormat('HH:mm:ss').format(_lastCheckAt!)}',
                                'Last check: ${DateFormat('HH:mm:ss').format(_lastCheckAt!)}',
                              ),
                        style: TextStyle(
                          fontSize: 12,
                          color: DmColors.mutedText(isDark),
                        ),
                      ),
                    ],
                  ),
                ),
                DmCard(
                  child: _loading
                      ? const Center(child: CircularProgressIndicator())
                       : _statusContent(
                           isFr: isFr,
                           order: order,
                           isPaid: isPaid,
                           isFailed: isFailed,
                           isDark: isDark,
                         ),
                ),
                if (_error != null && _error!.isNotEmpty)
                  DmCard(
                    child: Text(
                      _error!,
                      style: TextStyle(
                        color: isDark ? DmColors.errorDark : DmColors.errorLight,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                DmCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      DmPrimaryButton(
                        label: isPaid
                            ? _tr(isFr, 'Ouvrir le detail commande',
                                'Open order details')
                            : _tr(isFr, 'Verifier maintenant', 'Check now'),
                        onPressed: () => isPaid
                            ? context.go('/orders/${widget.orderId}')
                            : _refreshOrder(),
                        height: 46,
                      ),
                      const SizedBox(height: 10),
                      if (!isPaid &&
                          (widget.provider.toLowerCase().contains('paypal') ||
                              widget.provider.toLowerCase().contains('stripe')))
                        DmSecondaryButton(
                          label: _tr(
                              isFr, 'Rouvrir $provider', 'Re-open $provider'),
                          onPressed:
                              _reopeningProvider ? null : _reopenProvider,
                          height: 44,
                        ),
                      if (!isPaid) ...[
                        const SizedBox(height: 10),
                        DmSecondaryButton(
                          label: _tr(
                              isFr, 'Contacter le support', 'Contact support'),
                          onPressed: _openSupportWithPrefill,
                          height: 44,
                        ),
                      ],
                      const SizedBox(height: 10),
                      DmSecondaryButton(
                        label:
                            _tr(isFr, 'Retour aux commandes', 'Back to orders'),
                        onPressed: () => context.go('/orders'),
                        height: 44,
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

  Widget _statusContent({
    required bool isFr,
    required OrderDto? order,
    required bool isPaid,
    required bool isFailed,
    required bool isDark,
  }) {
    if (order == null) {
      return Text(_tr(
          isFr, 'Impossible de charger la commande.', 'Unable to load order.'));
    }

    final color = isPaid
        ? DmColors.successLight
        : isFailed
            ? DmColors.errorLight
            : DmColors.doorOrange;
    final icon = isPaid
        ? Icons.check_circle
        : isFailed
            ? Icons.error
            : Icons.schedule;
    final title = isPaid
        ? _tr(isFr, 'Paiement confirme', 'Payment confirmed')
        : isFailed
            ? _tr(isFr, 'Paiement echoue', 'Payment failed')
            : _tr(isFr, 'Paiement en attente', 'Payment pending');
    final body = isPaid
        ? _tr(
            isFr,
            'Votre paiement est confirme. La commande peut etre preparee.',
            'Your payment is confirmed. The order can be fulfilled.',
          )
        : isFailed
            ? _tr(
                isFr,
                'Le paiement a echoue. Reessayez ou contactez le support.',
                'Payment failed. Retry or contact support.',
              )
            : _tr(
                isFr,
                'Le paiement n est pas encore confirme. Revenez ici apres validation sur le fournisseur.',
                'Payment is not confirmed yet. Come back here after validating on provider.',
              );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                title,
                style: TextStyle(
                    fontSize: 16, fontWeight: FontWeight.w700, color: color),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text(body),
        const SizedBox(height: 10),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: DmColors.iconBg(isDark),
            borderRadius: DmRadius.r12,
            border: Border.all(color: DmColors.border(isDark)),
          ),
          child: Text(
            _tr(
              isFr,
              'Statut API: ${order.paymentStatus}',
              'API status: ${order.paymentStatus}',
            ),
            style: TextStyle(
              fontSize: 12,
              color: DmColors.mutedText(isDark),
            ),
          ),
        ),
      ],
    );
  }

  Future<void> _refreshOrder({bool initial = false}) async {
    if (!mounted) return;
    if (initial) {
      setState(() => _loading = true);
    }

    try {
      final order = await ref.read(ordersApiProvider).getById(widget.orderId);
      if (!mounted) return;
      setState(() {
        _order = order;
        _lastCheckAt = DateTime.now();
        _error = null;
        _loading = false;
      });
      ref.invalidate(orderDetailsProvider(widget.orderId));
      ref.invalidate(ordersProvider);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Future<void> _pollIfNeeded() async {
    final status = (_order?.paymentStatus ?? '').trim().toLowerCase();
    if (status == 'paid') {
      return;
    }
    await _refreshOrder();
  }

  Future<void> _reopenProvider() async {
    if (_reopeningProvider) return;
    setState(() => _reopeningProvider = true);
    try {
      final isFr =
          Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
      final api = ref.read(paymentsApiProvider);
      final result = await CheckoutHardeningService.openExternalCheckoutWithFallback(
        preferredProvider: _providerLabel(widget.provider),
        createCheckoutUrl: (provider) async {
          if (provider == 'Stripe') {
            return (await api.createStripeCheckout(widget.orderId)).url;
          }
          return (await api.createPayPalCheckout(widget.orderId)).url;
        },
      );
      if (!result.launched && mounted) {
        setState(() {
          _error = _tr(
              isFr,
              'Impossible d ouvrir le fournisseur de paiement. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}',
              'Unable to open payment provider. ${CheckoutHardeningService.summarizeAttempts(result.attempts)}');
        });
      } else if (result.usedFallback && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(_tr(
              isFr,
              'Bascule automatique vers ${result.providerUsed}.',
              'Automatic fallback to ${result.providerUsed}.',
            )),
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) {
        setState(() => _reopeningProvider = false);
      }
    }
  }

  void _openSupportWithPrefill() {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final orderCode = widget.orderId.substring(0, 8).toUpperCase();
    final status = (_order?.paymentStatus ?? 'unknown').toUpperCase();
    final subject = isFr
        ? 'Incident paiement commande $orderCode'
        : 'Payment issue for order $orderCode';
    final message = isFr
        ? 'Bonjour, j ai besoin d aide pour reprendre un paiement.\nCommande: $orderCode\nStatut paiement: $status\nFournisseur: ${_providerLabel(widget.provider)}'
        : 'Hello, I need help to resume a payment.\nOrder: $orderCode\nPayment status: $status\nProvider: ${_providerLabel(widget.provider)}';
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

  String _providerLabel(String raw) {
    final normalized = raw.trim().toLowerCase();
    if (normalized.contains('stripe')) {
      return 'Stripe';
    }
    if (normalized.contains('paypal')) {
      return 'PayPal';
    }
    if (normalized.isEmpty) {
      return 'PayPal';
    }
    return raw;
  }

  String _tr(bool isFr, String fr, String en) => isFr ? fr : en;
}
