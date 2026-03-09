import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/models/orders.dart';
import '../../core/models/returns.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';

class ReturnsScreen extends ConsumerStatefulWidget {
  final String? initialOrderId;

  const ReturnsScreen({super.key, this.initialOrderId});

  @override
  ConsumerState<ReturnsScreen> createState() => _ReturnsScreenState();
}

class _ReturnsScreenState extends ConsumerState<ReturnsScreen> {
  final _reasonController = TextEditingController();
  final _commentController = TextEditingController();
  final _amountController = TextEditingController();

  bool _loading = true;
  bool _submitting = false;
  String? _error;

  final List<OrderDto> _eligibleOrders = <OrderDto>[];
  String? _selectedOrderId;
  final List<ReturnReasonDto> _reasons = <ReturnReasonDto>[];
  String? _selectedReasonCode;

  final List<ReturnRequestDto> _items = <ReturnRequestDto>[];
  String _statusFilter = '';
  int _page = 1;
  static const int _pageSize = 20;
  int _total = 0;

  @override
  void initState() {
    super.initState();
    _loadAll();
  }

  @override
  void dispose() {
    _reasonController.dispose();
    _commentController.dispose();
    _amountController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Retours', 'Returns'),
            subtitle: t(
              context,
              'Demander un retour et suivre le traitement',
              'Request a return and track progress',
            ),
            showBack: true,
            onBack: () => Navigator.of(context).maybePop(),
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
                      children: [
                        if (_error != null)
                          DmCard(
                            borderColor: (isDark
                                    ? DmColors.errorDark
                                    : DmColors.errorLight)
                                .withAlpha(120),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  _error!,
                                  style: TextStyle(
                                    color: isDark
                                        ? DmColors.errorDark
                                        : DmColors.errorLight,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                const SizedBox(height: 8),
                                DmSecondaryButton(
                                  label: t(context, 'Reessayer', 'Retry'),
                                  height: 42,
                                  onPressed: _refresh,
                                ),
                              ],
                            ),
                          ),
                        _requestCard(context),
                        const SizedBox(height: 12),
                        _trackingHeader(context),
                        const SizedBox(height: 10),
                        if (_items.isEmpty)
                          DmCard(
                            child: Text(
                              t(
                                context,
                                'Aucune demande de retour.',
                                'No return request yet.',
                              ),
                              style: TextStyle(
                                color: DmColors.mutedText(isDark),
                              ),
                            ),
                          ),
                        ..._items.map((item) => _returnCard(context, item)),
                        if (_total > _pageSize) ...[
                          const SizedBox(height: 12),
                          _pagination(context),
                        ],
                      ],
                    ),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _requestCard(BuildContext context) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t(context, 'Demander un retour', 'Request a return'),
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 6),
          Text(
            t(
              context,
              'Seules les commandes payees et livrees sont eligibles.',
              'Only paid and delivered orders are eligible.',
            ),
            style:
                TextStyle(fontSize: 12, color: DmColors.mutedText(isDark)),
          ),
          const SizedBox(height: 12),
          if (_eligibleOrders.isEmpty)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: DmColors.iconBg(isDark),
                borderRadius: DmRadius.r12,
                border: Border.all(color: DmColors.border(isDark)),
              ),
              child: Text(
                t(
                  context,
                  'Aucune commande eligible pour un retour.',
                  'No eligible order for return.',
                ),
                style: TextStyle(color: DmColors.mutedText(isDark)),
              ),
            )
          else ...[
            DropdownButtonFormField<String>(
              key: ValueKey(_selectedOrderId),
              initialValue: _selectedOrderId,
              decoration: InputDecoration(
                labelText: t(context, 'Commande', 'Order'),
                border: const OutlineInputBorder(),
              ),
              items: _eligibleOrders
                  .map(
                    (order) => DropdownMenuItem<String>(
                      value: order.id,
                      child: Text(
                        '${_buildOrderCode(order.id)} - ${order.currency} ${order.totalAmount.toStringAsFixed(2)}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (value) => setState(() => _selectedOrderId = value),
            ),
            const SizedBox(height: 10),
            if (_reasons.isNotEmpty)
              DropdownButtonFormField<String>(
                initialValue: _selectedReasonCode,
                decoration: InputDecoration(
                  labelText: t(context, 'Motif de retour', 'Return reason'),
                  border: const OutlineInputBorder(),
                ),
                items: _reasons
                    .map(
                      (reason) => DropdownMenuItem<String>(
                        value: reason.code,
                        child: Text(
                          _reasonLabel(context, reason),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                onChanged: (value) =>
                    setState(() => _selectedReasonCode = value),
              )
            else
              TextField(
                controller: _reasonController,
                maxLength: 120,
                decoration: InputDecoration(
                  labelText: t(context, 'Raison', 'Reason'),
                  border: const OutlineInputBorder(),
                ),
              ),
            if (_reasons.isNotEmpty && _selectedReasonCode != null) ...[
              const SizedBox(height: 6),
              Text(
                _reasonDescription(context, _selectedReasonCode!),
                style: TextStyle(
                  fontSize: 12,
                  color: DmColors.mutedText(isDark),
                ),
              ),
            ],
            const SizedBox(height: 10),
            TextField(
              controller: _commentController,
              maxLines: 3,
              maxLength: 1000,
              decoration: InputDecoration(
                labelText:
                    t(context, 'Commentaire (optionnel)', 'Comment (optional)'),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _amountController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: t(context, 'Montant demande (optionnel)',
                    'Requested amount (optional)'),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            DmPrimaryButton(
              label: _submitting
                  ? t(context, 'Envoi...', 'Submitting...')
                  : t(context, 'Envoyer la demande', 'Submit request'),
              height: 46,
              onPressed: _submitting ? null : _submit,
            ),
          ],
        ],
      ),
    );
  }

  Widget _trackingHeader(BuildContext context) {
    final t = _tr;
    final statuses = <MapEntry<String, String>>[
      MapEntry('', t(context, 'Tous', 'All')),
      const MapEntry('Requested', 'Requested'),
      const MapEntry('Approved', 'Approved'),
      const MapEntry('Rejected', 'Rejected'),
      const MapEntry('Refunded', 'Refunded'),
    ];

    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  t(context, 'Suivi des retours', 'Return tracking'),
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              DmSecondaryButton(
                label: t(context, 'Rafraichir', 'Refresh'),
                height: 40,
                width: 120,
                onPressed: _refreshTracking,
              ),
            ],
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: statuses
                .map(
                  (entry) => ChoiceChip(
                    selected: _statusFilter == entry.key,
                    label: Text(entry.value),
                    onSelected: (_) => _changeStatus(entry.key),
                  ),
                )
                .toList(),
          ),
        ],
      ),
    );
  }

  Widget _returnCard(BuildContext context, ReturnRequestDto item) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final created =
        DateFormat('dd/MM/yyyy HH:mm').format(item.createdAtUtc.toLocal());

    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  item.orderNumber.isNotEmpty
                      ? item.orderNumber
                      : _buildOrderCode(item.orderId),
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
              _statusBadge(item.status),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            created,
            style: TextStyle(
              fontSize: 12,
              color: DmColors.mutedText(isDark),
            ),
          ),
          const SizedBox(height: 10),
          _kv(
            _tr(context, 'Raison', 'Reason'),
            item.reason,
          ),
          if ((item.reasonCode ?? '').trim().isNotEmpty)
            _kv(
              _tr(context, 'Code motif', 'Reason code'),
              item.reasonCode!.trim(),
            ),
          if ((item.comment ?? '').trim().isNotEmpty)
            _kv(
              _tr(context, 'Commentaire', 'Comment'),
              item.comment!.trim(),
            ),
          _kv(
            _tr(context, 'Demande', 'Requested'),
            '${item.currency} ${item.requestedAmount.toStringAsFixed(2)}',
          ),
          _kv(
            _tr(context, 'Approuve', 'Approved'),
            item.approvedAmount == null
                ? '-'
                : '${item.currency} ${item.approvedAmount!.toStringAsFixed(2)}',
          ),
          if ((item.adminNote ?? '').trim().isNotEmpty)
            _kv(
              _tr(context, 'Note admin', 'Admin note'),
              item.adminNote!.trim(),
            ),
          if (item.slaTargetAtUtc != null) ...[
            _kv(
              _tr(context, 'SLA', 'SLA'),
              DateFormat('dd/MM/yyyy HH:mm')
                  .format(item.slaTargetAtUtc!.toLocal()),
            ),
            if (item.isSlaBreached)
              Text(
                _tr(context, 'SLA depassee', 'SLA breached'),
                style: TextStyle(
                  color: isDark ? DmColors.errorDark : DmColors.errorLight,
                  fontWeight: FontWeight.w700,
                  fontSize: 12,
                ),
              ),
          ],
          const SizedBox(height: 8),
          DmSecondaryButton(
            label: _tr(context, 'Voir timeline', 'View timeline'),
            height: 40,
            onPressed: () => _showTimeline(item.id),
          ),
        ],
      ),
    );
  }

  Widget _pagination(BuildContext context) {
    final t = _tr;
    final totalPages = (_total / _pageSize).ceil().clamp(1, 9999);
    final canPrev = _page > 1;
    final canNext = _page < totalPages;

    return Row(
      children: [
        Expanded(
          child: DmSecondaryButton(
            label: t(context, 'Precedent', 'Previous'),
            height: 42,
            onPressed: canPrev ? () => _changePage(_page - 1) : null,
          ),
        ),
        const SizedBox(width: 12),
        Text('$_page / $totalPages'),
        const SizedBox(width: 12),
        Expanded(
          child: DmSecondaryButton(
            label: t(context, 'Suivant', 'Next'),
            height: 42,
            onPressed: canNext ? () => _changePage(_page + 1) : null,
          ),
        ),
      ],
    );
  }

  Widget _kv(String key, String value) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 96,
            child: Text(
              key,
              style: TextStyle(
                fontSize: 12,
                color: DmColors.mutedText(isDark),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }

  Widget _statusBadge(String status) {
    final color = _statusColor(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withAlpha(35),
        borderRadius: DmRadius.r12,
        border: Border.all(color: color.withAlpha(160)),
      ),
      child: Text(
        status,
        style: TextStyle(
          fontSize: 12,
          color: color,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }

  Color _statusColor(String status) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final normalized = status.trim().toLowerCase();
    if (normalized == 'requested') {
      return DmColors.iconFg(isDark);
    }
    if (normalized == 'approved') return DmColors.doorOrange;
    if (normalized == 'rejected') {
      return isDark ? DmColors.errorDark : DmColors.errorLight;
    }
    if (normalized == 'refunded') {
      return isDark ? DmColors.successDark : DmColors.successLight;
    }
    return DmColors.mutedText(isDark);
  }

  Future<void> _refresh() async {
    await _loadAll();
  }

  Future<void> _refreshTracking() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _loadReturns();
    } catch (e) {
      _error = _readableError(e);
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  Future<void> _loadAll() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _loadEligibleOrders();
      await _loadReasons();
      await _loadReturns();
    } catch (e) {
      _error = _readableError(e);
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  Future<void> _loadEligibleOrders() async {
    final api = ref.read(ordersApiProvider);
    final result = await api.getMine(page: 1, pageSize: 100);
    final eligible = result.items.where(_isReturnEligible).toList()
      ..sort((a, b) => b.createdAtUtc.compareTo(a.createdAtUtc));

    _eligibleOrders
      ..clear()
      ..addAll(eligible);

    final fromQuery = widget.initialOrderId;
    if (fromQuery != null &&
        _eligibleOrders.any((order) => order.id == fromQuery)) {
      _selectedOrderId = fromQuery;
      return;
    }

    if (_selectedOrderId != null &&
        _eligibleOrders.any((order) => order.id == _selectedOrderId)) {
      return;
    }

    _selectedOrderId =
        _eligibleOrders.isNotEmpty ? _eligibleOrders.first.id : null;
  }

  Future<void> _loadReasons() async {
    final api = ref.read(returnsApiProvider);
    final reasons = await api.getReasons();
    _reasons
      ..clear()
      ..addAll(reasons);

    if (_selectedReasonCode != null &&
        _reasons.any((r) => r.code == _selectedReasonCode)) {
      return;
    }

    _selectedReasonCode = _reasons.isNotEmpty ? _reasons.first.code : null;
  }

  Future<void> _loadReturns() async {
    final api = ref.read(returnsApiProvider);
    final result = await api.getMine(
      status: _statusFilter.isEmpty ? null : _statusFilter,
      page: _page,
      pageSize: _pageSize,
    );
    _items
      ..clear()
      ..addAll(result.items);
    _total = result.total;
  }

  bool _isReturnEligible(OrderDto order) {
    final payment = order.paymentStatus.trim().toLowerCase();
    final status = order.fulfillmentStatus.trim().isNotEmpty
        ? order.fulfillmentStatus.trim().toLowerCase()
        : order.status.trim().toLowerCase();

    final paid = payment == 'paid';
    final delivered = status == 'delivered' || status == 'completed';
    return paid && delivered;
  }

  Future<void> _submit() async {
    final t = _tr;
    final selectedOrderId = _selectedOrderId;
    ReturnReasonDto? selectedReason;
    if (_selectedReasonCode != null) {
      for (final reason in _reasons) {
        if (reason.code == _selectedReasonCode) {
          selectedReason = reason;
          break;
        }
      }
    }
    final reason = selectedReason != null
        ? _reasonLabel(context, selectedReason).trim()
        : _reasonController.text.trim();
    final comment = _commentController.text.trim();

    if (selectedOrderId == null || selectedOrderId.isEmpty) {
      _showSnack(t(context, 'Selectionnez une commande.', 'Select an order.'));
      return;
    }

    if (_reasons.isNotEmpty && selectedReason == null) {
      _showSnack(
          t(context, 'Selectionnez un motif.', 'Select a return reason.'));
      return;
    }

    if (reason.isEmpty) {
      _showSnack(t(context, 'Le motif est requis.', 'Reason is required.'));
      return;
    }

    final requestedAmount = _parseAmount(_amountController.text);
    if (requestedAmount != null && requestedAmount <= 0) {
      _showSnack(t(context, 'Montant invalide.', 'Invalid amount.'));
      return;
    }

    setState(() => _submitting = true);
    try {
      final api = ref.read(returnsApiProvider);
      await api.create(
        CreateReturnRequest(
          orderId: selectedOrderId,
          reasonCode: selectedReason?.code,
          reason: reason,
          comment: comment.isEmpty ? null : comment,
          requestedAmount: requestedAmount,
        ),
      );

      _reasonController.clear();
      _commentController.clear();
      _amountController.clear();
      if (_reasons.isNotEmpty) {
        _selectedReasonCode = _reasons.first.code;
      }
      _page = 1;

      await _loadReturns();
      if (!mounted) {
        return;
      }
      setState(() {});
      _showSnack(t(context, 'Demande envoyee.', 'Request submitted.'));
    } catch (e) {
      if (!mounted) {
        return;
      }
      _showSnack(_readableError(e));
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  Future<void> _changeStatus(String status) async {
    if (_statusFilter == status) {
      return;
    }

    setState(() {
      _statusFilter = status;
      _page = 1;
    });
    await _refreshTracking();
  }

  Future<void> _changePage(int page) async {
    if (page == _page || page <= 0) {
      return;
    }

    setState(() => _page = page);
    await _refreshTracking();
  }

  String _reasonLabel(BuildContext context, ReturnReasonDto reason) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? reason.titleFr : reason.titleEn;
  }

  String _reasonDescription(BuildContext context, String reasonCode) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    ReturnReasonDto? reason;
    for (final row in _reasons) {
      if (row.code == reasonCode) {
        reason = row;
        break;
      }
    }
    if (reason == null) {
      return '';
    }
    final desc = isFr ? reason.descriptionFr : reason.descriptionEn;
    final sla = reason.defaultSlaHours > 0
        ? (isFr
            ? 'SLA: ${reason.defaultSlaHours}h'
            : 'SLA: ${reason.defaultSlaHours}h')
        : '';
    if ((desc ?? '').trim().isEmpty) {
      return sla;
    }
    if (sla.isEmpty) {
      return desc!.trim();
    }
    return '${desc!.trim()} â€¢ $sla';
  }

  Future<void> _showTimeline(String returnId) async {
    final t = _tr;
    try {
      final events = await ref.read(returnsApiProvider).getTimeline(returnId);
      if (!mounted) {
        return;
      }
      final isDark = Theme.of(context).brightness == Brightness.dark;

      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (ctx) => SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 20),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  t(ctx, 'Timeline du retour', 'Return timeline'),
                  style: Theme.of(ctx).textTheme.headlineSmall,
                ),
                const SizedBox(height: 12),
                if (events.isEmpty)
                  Text(
                    t(ctx, 'Aucun evenement.', 'No events.'),
                    style: TextStyle(color: DmColors.mutedText(isDark)),
                  )
                else
                  SizedBox(
                    height: 320,
                    child: ListView.separated(
                      itemCount: events.length,
                      separatorBuilder: (_, __) => const Divider(height: 18),
                      itemBuilder: (ctx, index) {
                        final event = events[index];
                        final at = DateFormat('dd/MM/yyyy HH:mm')
                            .format(event.changedAtUtc.toLocal());
                        return Container(
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: isDark
                                ? DmColors.surfaceAltDark
                                : DmColors.iconBgLight,
                            borderRadius: DmRadius.r12,
                            border: Border.all(
                              color: isDark
                                  ? DmColors.borderDark
                                  : DmColors.borderLight,
                            ),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                '${event.oldStatus} -> ${event.newStatus}',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                at,
                                style: TextStyle(
                                  fontSize: 12,
                                  color: DmColors.mutedText(isDark),
                                ),
                              ),
                              if ((event.changedBy ?? '').trim().isNotEmpty)
                                Text(
                                  event.changedBy!.trim(),
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: DmColors.mutedText(isDark),
                                  ),
                                ),
                              if ((event.note ?? '').trim().isNotEmpty) ...[
                                const SizedBox(height: 4),
                                Text(event.note!.trim()),
                              ],
                            ],
                          ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          ),
        ),
      );
    } catch (e) {
      _showSnack(_readableError(e));
    }
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  double? _parseAmount(String raw) {
    final value = raw.trim();
    if (value.isEmpty) {
      return null;
    }
    return double.tryParse(value.replaceAll(',', '.'));
  }

  String _readableError(Object error) {
    final text = error.toString();
    if (text.startsWith('ApiException')) {
      final idx = text.indexOf(':');
      if (idx > 0 && idx < text.length - 1) {
        return text.substring(idx + 1).trim();
      }
    }

    if (text.startsWith('Exception:')) {
      return text.substring('Exception:'.length).trim();
    }

    return text;
  }

  static String _buildOrderCode(String orderId) {
    final compact = orderId.replaceAll('-', '');
    final take = compact.length >= 6 ? compact.substring(0, 6) : compact;
    return 'DM${take.toUpperCase()}';
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}

