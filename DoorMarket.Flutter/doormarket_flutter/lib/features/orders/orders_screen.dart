import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/models/orders.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/services/user_error_service.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import 'orders_provider.dart';

class OrdersScreen extends ConsumerStatefulWidget {
  const OrdersScreen({super.key});

  @override
  ConsumerState<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends ConsumerState<OrdersScreen>
    with AutomaticKeepAliveClientMixin {
  bool _showCompleted = false;
  bool _showPendingOnly = false;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final data = ref.watch(ordersProvider);
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Commandes', 'Orders'),
            subtitle: t(context, 'Suivi de vos commandes', 'Track your orders'),
          ),
          Expanded(
            child: data.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (err, _) => _ordersErrorState(context, err),
              data: (orders) {
                final ongoing = orders.where((o) => _isOngoing(o)).toList();
                final done = orders.where((o) => !_isOngoing(o)).toList();
                final pendingPayments =
                    orders.where((o) => _needsPaymentAction(o)).toList();
                final current = _showPendingOnly
                    ? pendingPayments
                    : (_showCompleted ? done : ongoing);

                return RefreshIndicator(
                  onRefresh: _refreshOrders,
                  child: ListView(
                    key: const PageStorageKey<String>('orders-scroll'),
                    padding: const EdgeInsets.all(24),
                    children: [
                      DmCard(
                        child: Row(
                          children: [
                            _iconBadge(context, Icons.receipt_long),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                      t(context, 'Historique des commandes',
                                          'Order history'),
                                      style: const TextStyle(
                                          fontWeight: FontWeight.w600)),
                                  const SizedBox(height: 4),
                                  Text(
                                    t(context, 'Suivez vos commandes recentes',
                                        'Track your recent orders'),
                                    style: TextStyle(
                                      color: DmColors.mutedText(isDark),
                                      fontSize: 12,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      DmCard(
                        child: Row(
                          children: [
                            _statBlock(
                                context,
                                t(context, 'En cours', 'Ongoing'),
                                ongoing.length),
                            const SizedBox(width: 12),
                            Container(
                                width: 1,
                                height: 32,
                                color: DmColors.border(isDark)),
                            const SizedBox(width: 12),
                            _statBlock(
                                context,
                                t(context, 'Terminees', 'Completed'),
                                done.length),
                          ],
                        ),
                      ),
                      if (pendingPayments.isNotEmpty)
                        DmCard(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                t(
                                  context,
                                  'Paiements a finaliser (${pendingPayments.length})',
                                  'Payments to complete (${pendingPayments.length})',
                                ),
                                style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    color: DmColors.doorOrange),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                t(
                                  context,
                                  'Certaines commandes ne sont pas encore reglees. Reprenez le paiement pour lancer la preparation.',
                                  'Some orders are not paid yet. Resume payment to start fulfillment.',
                                ),
                                style: TextStyle(
                                  fontSize: 12,
                                  color: DmColors.mutedText(isDark),
                                ),
                              ),
                              const SizedBox(height: 10),
                              DmPrimaryButton(
                                label: t(context, 'Reprendre le paiement',
                                    'Resume payment'),
                                height: 44,
                                onPressed: () => context.push(
                                    '/orders/${pendingPayments.first.id}'),
                              ),
                            ],
                          ),
                        ),
                      Row(
                        children: [
                          Expanded(
                            child: _tabButton(
                              label: t(context, 'En cours', 'Ongoing'),
                              selected: !_showCompleted && !_showPendingOnly,
                              onTap: () => setState(() {
                                _showCompleted = false;
                                _showPendingOnly = false;
                              }),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: _tabButton(
                              label: t(context, 'Terminees', 'Completed'),
                              selected: _showCompleted && !_showPendingOnly,
                              onTap: () => setState(() {
                                _showCompleted = true;
                                _showPendingOnly = false;
                              }),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      _tabButton(
                        label: t(
                          context,
                          'Paiements a finaliser (${pendingPayments.length})',
                          'Payments to complete (${pendingPayments.length})',
                        ),
                        selected: _showPendingOnly,
                        onTap: () => setState(
                            () => _showPendingOnly = !_showPendingOnly),
                      ),
                      const SizedBox(height: 12),
                      if (current.isEmpty)
                        DmCard(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                  _showPendingOnly
                                      ? t(context, 'Aucun paiement a reprendre',
                                          'No payment to resume')
                                      : _showCompleted
                                          ? t(
                                              context,
                                              'Aucune commande terminee',
                                              'No completed order')
                                          : t(
                                              context,
                                              'Aucune commande en cours',
                                              'No ongoing order'),
                                  style: const TextStyle(
                                      fontWeight: FontWeight.w600)),
                              const SizedBox(height: 6),
                              Text(
                                  t(context, 'Vos commandes apparaitront ici.',
                                      'Your orders will appear here.'),
                                  style: TextStyle(
                                      color: DmColors.mutedText(isDark))),
                            ],
                          ),
                        ),
                      ...current.map((order) => RepaintBoundary(
                            child: _orderCard(context, order),
                          )),
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  @override
  bool get wantKeepAlive => true;

  Future<void> _refreshOrders() async {
    ref.invalidate(ordersProvider);
    await ref.read(ordersProvider.future);
  }

  Widget _ordersErrorState(BuildContext context, Object err) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final message = UserErrorService.message(
      context,
      err,
      fallbackFr: 'Le chargement des commandes est indisponible.',
      fallbackEn: 'Orders loading is currently unavailable.',
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
                height: 42,
                label: _tr(context, 'Reessayer', 'Retry'),
                onPressed: () => _refreshOrders(),
              ),
            ],
          ),
        ),
      ),
    );
  }

  bool _isOngoing(OrderDto order) {
    final status = order.fulfillmentStatus.isNotEmpty
        ? order.fulfillmentStatus
        : order.status;
    final normalized = status.toLowerCase();
    return !(normalized.contains('deliver') ||
        normalized.contains('complete') ||
        normalized.contains('cancel') ||
        normalized.contains('fail'));
  }

  bool _needsPaymentAction(OrderDto order) {
    final paymentStatus = order.paymentStatus.trim().toLowerCase();
    if (paymentStatus.isEmpty) {
      return false;
    }

    return paymentStatus != 'paid';
  }

  Widget _iconBadge(BuildContext context, IconData icon) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 36,
      width: 36,
      decoration: BoxDecoration(
        color: DmColors.iconBg(isDark),
        borderRadius: DmRadius.r12,
      ),
      child: Icon(
        icon,
        color: DmColors.iconFg(isDark),
        size: 16,
      ),
    );
  }

  Widget _statBlock(BuildContext context, String label, int value) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label,
              style: TextStyle(
                fontSize: 12,
                color: DmColors.mutedText(isDark),
              )),
          const SizedBox(height: 2),
          Text(value.toString(),
              style:
                  const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }

  Widget _tabButton(
      {required String label,
      required bool selected,
      required VoidCallback onTap}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = selected
        ? DmColors.doorBlue
        : (isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight);
    final textColor = selected ? Colors.white : (DmColors.iconFg(isDark));
    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 48,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: bg,
          borderRadius: DmRadius.r16,
          border: Border.all(
              color: selected ? DmColors.doorBlue : DmColors.border(isDark)),
        ),
        child: Text(label,
            style: TextStyle(fontWeight: FontWeight.w600, color: textColor)),
      ),
    );
  }

  Widget _orderCard(BuildContext context, OrderDto order) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final status = order.fulfillmentStatus.isNotEmpty
        ? order.fulfillmentStatus
        : order.status;
    final isDelivered = status.toLowerCase() == 'delivered';
    final canRequestReturn = _canRequestReturn(order);
    final dateLabel =
        DateFormat('dd/MM/yyyy').format(order.createdAtUtc.toLocal());
    final needsPaymentAction = _needsPaymentAction(order);

    return DmCard(
      child: InkWell(
        onTap: () => context.push('/orders/${order.id}'),
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
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      Text(dateLabel,
                          style: TextStyle(
                            fontSize: 12,
                            color: DmColors.mutedText(isDark),
                          )),
                    ],
                  ),
                ),
                _statusBadge(context, status, isDelivered),
                const SizedBox(width: 6),
                Icon(Icons.chevron_right, color: DmColors.mutedText(isDark)),
              ],
            ),
            if (needsPaymentAction) ...[
              const SizedBox(height: 8),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: DmColors.warningSurface(false),
                  borderRadius: DmRadius.r12,
                  border: Border.all(color: DmColors.doorOrange.withAlpha(150)),
                ),
                child: Text(
                  _tr(context, 'Paiement non finalise. Touchez pour reprendre.',
                      'Payment not completed. Tap to resume.'),
                  style: const TextStyle(
                      fontSize: 12,
                      color: DmColors.doorOrange,
                      fontWeight: FontWeight.w600),
                ),
              ),
            ],
            if (order.paymentProvider.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(
                  _tr(context, 'Paiement: ${order.paymentProvider}',
                      'Payment: ${order.paymentProvider}'),
                  style: TextStyle(
                    fontSize: 12,
                    color: DmColors.mutedText(isDark),
                  )),
            ],
            const SizedBox(height: 10),
            const Divider(),
            const SizedBox(height: 6),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(_tr(context, 'Total', 'Total'),
                    style: TextStyle(
                      fontSize: 12,
                      color: DmColors.mutedText(isDark),
                    )),
                Text(
                  '${order.currency} ${order.totalAmount.toStringAsFixed(2)}',
                  style: const TextStyle(
                      fontWeight: FontWeight.w700, color: DmColors.doorOrange),
                ),
              ],
            ),
            if (canRequestReturn) ...[
              const SizedBox(height: 10),
              Align(
                alignment: Alignment.centerRight,
                child: InkWell(
                  borderRadius: DmRadius.r12,
                  onTap: () => context.push('/returns?orderId=${order.id}'),
                  child: Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: DmColors.iconBg(isDark),
                      borderRadius: DmRadius.r12,
                      border: Border.all(color: DmColors.border(isDark)),
                    ),
                    child: Text(
                      _tr(context, 'Demander retour', 'Request return'),
                      style: TextStyle(
                        fontSize: 12,
                        color: DmColors.iconFg(isDark),
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  bool _canRequestReturn(OrderDto order) {
    final payment = order.paymentStatus.trim().toLowerCase();
    final status = order.fulfillmentStatus.trim().isNotEmpty
        ? order.fulfillmentStatus.trim().toLowerCase()
        : order.status.trim().toLowerCase();
    return payment == 'paid' &&
        (status == 'delivered' || status == 'completed');
  }

  Widget _statusBadge(BuildContext context, String status, bool isDelivered) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
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

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}
