import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/admin_notifications.dart';
import '../../core/models/orders.dart';
import '../../core/notifications/notification_preferences_controller.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/theme/spacing.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import 'notification_counters_provider.dart';
import 'notification_feed_logic.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({
    super.key,
    this.initialUnreadOnly = false,
  });

  final bool initialUnreadOnly;

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  bool _bootstrappedAdmin = false;
  bool _bootstrappedClient = false;
  bool _loading = false;
  bool _busy = false;
  String? _error;

  final DateTime _fromUtc =
      DateTime.now().toUtc().subtract(const Duration(days: 30));
  final DateTime _toUtc = DateTime.now().toUtc();
  String _typeFilter = '';
  String _statusFilter = '';
  bool _includeAcknowledged = false;
  int _minFailures = 1;
  int _incidentsTake = 20;
  int _page = 1;
  final int _pageSize = 20;
  int _total = 0;

  List<TransactionNotificationSummaryDto> _summary = const [];
  List<NotificationIncidentDto> _incidents = const [];
  List<TransactionNotificationDto> _rows = const [];
  bool _clientLoading = false;
  String? _clientError;
  List<_ClientNotificationItem> _clientFeed = const [];
  bool _showUnreadOnly = false;
  bool _showActionableOnly = false;
  String _categoryFilter = 'all';
  DateTime? _clientLastLoadedAt;
  DateTime? _adminLastLoadedAt;
  int _lastTickClient = -1;
  int _lastTickAdmin = -1;

  @override
  void initState() {
    super.initState();
    _showUnreadOnly = widget.initialUnreadOnly;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final role = ref.read(authControllerProvider).me?.role;
      _bootstrapForRole(_isAdminRole(role));
    });
  }

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final auth = ref.watch(authControllerProvider);
    final notificationPrefs = ref.watch(notificationPreferencesProvider);
    ref.listen(authControllerProvider, (previous, next) {
      _bootstrapForRole(_isAdminRole(next.me?.role));
    });
    ref.listen(notificationRefreshTickProvider, (previous, next) {
      final refreshTick =
          next.maybeWhen(data: (value) => value, orElse: () => 0);
      _handleRefreshTick(refreshTick);
    });
    final role = auth.me?.role;
    final isAdmin = _isAdminRole(role);
    if (!isAdmin &&
        !_clientLoading &&
        _clientFeed.isEmpty &&
        _clientLastLoadedAt == null &&
        _clientError == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || _clientLoading) return;
        unawaited(_loadClientData());
      });
    }

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Notifications', 'Notifications'),
            subtitle: isAdmin
                ? t(
                    context,
                    'Centre operationnel des notifications transactionnelles',
                    'Transactional notification operations center')
                : t(context, 'Suivez vos activites recentes',
                    'Track your recent activity'),
            showBack: false,
          ),
          Expanded(
            child: isAdmin
                ? _buildAdminCenter(context)
                : _buildClientView(context, notificationPrefs),
          ),
        ],
      ),
    );
  }

  bool? _lastRoleIsAdmin;

  void _bootstrapForRole(bool isAdmin) {
    if (!mounted) return;

    if (_lastRoleIsAdmin == isAdmin) {
      if (isAdmin && !_bootstrappedAdmin && !_loading && !_busy) {
        _bootstrappedAdmin = true;
        unawaited(_loadAdminData());
      } else if (!isAdmin &&
          !_bootstrappedClient &&
          !_clientLoading &&
          !_busy) {
        _bootstrappedClient = true;
        unawaited(_loadClientData());
      }
      return;
    }

    _lastRoleIsAdmin = isAdmin;
    if (isAdmin) {
      _bootstrappedAdmin = true;
      _lastTickAdmin = -1;
      if (!_loading && !_busy) {
        unawaited(_loadAdminData());
      }
      return;
    }

    _bootstrappedClient = true;
    _lastTickClient = -1;
    if (!_clientLoading && !_busy) {
      unawaited(_loadClientData());
    }
  }

  void _handleRefreshTick(int refreshTick) {
    if (!mounted || refreshTick <= 0) {
      return;
    }

    final isAdmin = _isAdminRole(ref.read(authControllerProvider).me?.role);
    if (isAdmin) {
      if (!_bootstrappedAdmin ||
          _loading ||
          _busy ||
          refreshTick == _lastTickAdmin) {
        return;
      }
      _lastTickAdmin = refreshTick;
      unawaited(_loadAdminData());
      return;
    }

    if (!_bootstrappedClient ||
        _clientLoading ||
        _busy ||
        refreshTick == _lastTickClient) {
      return;
    }
    _lastTickClient = refreshTick;
    unawaited(_loadClientData());
  }

  Widget _buildAdminCenter(BuildContext context) {
    final t = _tr;
    if (_loading && _summary.isEmpty && _incidents.isEmpty && _rows.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    return RefreshIndicator(
      onRefresh: _loadAdminData,
      child: ListView(
        padding: const EdgeInsets.all(DmSpacing.xxl),
        children: [
          DmCard(
            child: Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                _buildTypeFilter(context),
                _buildStatusFilter(context),
                SizedBox(
                  width: 160,
                  child: DropdownButtonFormField<int>(
                    initialValue: _minFailures,
                    decoration: InputDecoration(
                      labelText: t(context, 'Min echec incidents',
                          'Incident min failures'),
                      border: const OutlineInputBorder(),
                    ),
                    items: const [1, 2, 3, 5, 10]
                        .map((value) => DropdownMenuItem<int>(
                            value: value, child: Text(value.toString())))
                        .toList(),
                    onChanged: (value) =>
                        setState(() => _minFailures = value ?? 1),
                  ),
                ),
                SizedBox(
                  width: 160,
                  child: DropdownButtonFormField<int>(
                    initialValue: _incidentsTake,
                    decoration: InputDecoration(
                      labelText: t(context, 'Incidents max', 'Max incidents'),
                      border: const OutlineInputBorder(),
                    ),
                    items: const [10, 20, 50, 100]
                        .map((value) => DropdownMenuItem<int>(
                            value: value, child: Text(value.toString())))
                        .toList(),
                    onChanged: (value) =>
                        setState(() => _incidentsTake = value ?? 20),
                  ),
                ),
                SizedBox(
                  width: 190,
                  child: SwitchListTile(
                    dense: true,
                    value: _includeAcknowledged,
                    contentPadding: EdgeInsets.zero,
                    title: Text(
                        t(context, 'Inclure acquittes', 'Show acknowledged')),
                    onChanged: (value) =>
                        setState(() => _includeAcknowledged = value),
                  ),
                ),
                DmPrimaryButton(
                  width: 140,
                  label: t(context, 'Appliquer', 'Apply'),
                  onPressed: _busy
                      ? null
                      : () async {
                          setState(() => _page = 1);
                          await _loadAdminData();
                        },
                ),
                OutlinedButton.icon(
                  onPressed: _busy ? null : _retryBulk,
                  icon: const Icon(Icons.refresh),
                  label: Text(t(context, 'Retry global', 'Bulk retry')),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _buildSyncStatusCard(
            context,
            isAdmin: true,
            lastLoadedAt: _adminLastLoadedAt,
            onRefresh: _loadAdminData,
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            DmCard(
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          ],
          const SizedBox(height: 16),
          _summarySection(context),
          const SizedBox(height: 16),
          _incidentsSection(context),
          const SizedBox(height: 16),
          _transactionsSection(context),
        ],
      ),
    );
  }

  Widget _summarySection(BuildContext context) {
    final t = _tr;
    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t(context, 'Resume notifications', 'Notification summary'),
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 12),
          if (_summary.isEmpty)
            Text(t(context, 'Aucune donnee', 'No data'))
          else
            Wrap(
              spacing: 10,
              runSpacing: 10,
              children: _summary
                  .map(
                    (item) => Container(
                      width: 190,
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Theme.of(context).brightness == Brightness.dark
                            ? DmColors.surfaceAltDark
                            : DmColors.surfaceAltLight,
                        borderRadius: DmRadius.r12,
                        border: Border.all(
                          color: Theme.of(context).brightness == Brightness.dark
                              ? DmColors.borderDark
                              : DmColors.borderLight,
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(item.notificationType,
                              style:
                                  const TextStyle(fontWeight: FontWeight.w600)),
                          const SizedBox(height: 6),
                          Text(
                              '${t(context, 'Total', 'Total')}: ${item.total}'),
                          Text('${t(context, 'Envoye', 'Sent')}: ${item.sent}'),
                          Text(
                              '${t(context, 'Echec', 'Failed')}: ${item.failed}'),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
        ],
      ),
    );
  }

  Widget _incidentsSection(BuildContext context) {
    final t = _tr;
    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t(context, 'Incidents non resolus', 'Unresolved incidents'),
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 12),
          if (_incidents.isEmpty)
            Text(t(context, 'Aucun incident', 'No incidents'))
          else
            Column(
              children: _incidents
                  .map(
                    (item) => Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          borderRadius: DmRadius.r12,
                          border: Border.all(
                            color:
                                Theme.of(context).brightness == Brightness.dark
                                    ? DmColors.borderDark
                                    : DmColors.borderLight,
                          ),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('${item.notificationType} - ${item.recipient}',
                                style: const TextStyle(
                                    fontWeight: FontWeight.w600)),
                            const SizedBox(height: 4),
                            Text(
                              '${t(context, 'Echecs', 'Failures')}: ${item.failedCount}  |  ${t(context, 'Dernier', 'Last')}: ${_fmtDate(item.lastAttemptUtc)}',
                            ),
                            if ((item.lastError ?? '').trim().isNotEmpty) ...[
                              const SizedBox(height: 4),
                              Text(item.lastError!,
                                  style: Theme.of(context).textTheme.bodySmall),
                            ],
                            if (item.acknowledged) ...[
                              const SizedBox(height: 4),
                              Text(
                                '${t(context, 'Acquitte par', 'Acknowledged by')} ${item.acknowledgedBy ?? 'admin'} - ${item.acknowledgedAtUtc != null ? _fmtDate(item.acknowledgedAtUtc!) : '-'}',
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                            ],
                            const SizedBox(height: 8),
                            Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                OutlinedButton.icon(
                                  onPressed: _busy
                                      ? null
                                      : () => _retryOne(item.lastLogId),
                                  icon: const Icon(Icons.refresh),
                                  label: Text(t(context, 'Retry', 'Retry')),
                                ),
                                if (item.acknowledged)
                                  OutlinedButton.icon(
                                    onPressed: _busy
                                        ? null
                                        : () => _reopenIncident(item.lastLogId),
                                    icon: const Icon(Icons.undo),
                                    label:
                                        Text(t(context, 'Reouvrir', 'Reopen')),
                                  )
                                else
                                  OutlinedButton.icon(
                                    onPressed: _busy
                                        ? null
                                        : () => _acknowledgeIncident(
                                            item.lastLogId),
                                    icon: const Icon(Icons.task_alt),
                                    label: Text(
                                        t(context, 'Acquitter', 'Acknowledge')),
                                  ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  )
                  .toList(),
            ),
        ],
      ),
    );
  }

  Widget _transactionsSection(BuildContext context) {
    final t = _tr;
    return DmCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t(context, 'Transactions notifications',
                'Notification transactions'),
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 12),
          if (_rows.isEmpty)
            Text(t(context, 'Aucune tentative', 'No attempts'))
          else
            Column(
              children: _rows
                  .map(
                    (row) => ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      title: Text('${row.notificationType} - ${row.recipient}'),
                      subtitle: Text(
                          '${_fmtDate(row.attemptedAtUtc)} - ${row.subject}'),
                      trailing: _statusBadge(context, row.status),
                    ),
                  )
                  .toList(),
            ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('${t(context, 'Total', 'Total')}: $_total'),
              Wrap(
                spacing: 8,
                children: [
                  OutlinedButton(
                    onPressed: _busy || _page <= 1
                        ? null
                        : () async {
                            setState(() => _page -= 1);
                            await _loadAdminData();
                          },
                    child: Text(t(context, 'Prec', 'Prev')),
                  ),
                  OutlinedButton(
                    onPressed: _busy || (_page * _pageSize) >= _total
                        ? null
                        : () async {
                            setState(() => _page += 1);
                            await _loadAdminData();
                          },
                    child: Text(t(context, 'Suiv', 'Next')),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildClientView(
    BuildContext context,
    NotificationPreferencesController prefs,
  ) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    var filteredFeed = _clientFeed
        .where((item) => prefs.allowEventKind(_kindKey(item.kind)))
        .toList();
    filteredFeed = filteredFeed
        .where((item) => _matchesCategory(item.kind, _categoryFilter))
        .toList();
    if (_showActionableOnly) {
      filteredFeed =
          filteredFeed.where((item) => _isActionable(item.kind)).toList();
    }
    filteredFeed.sort((a, b) {
      final aRead = prefs.isRead(_eventId(a));
      final bRead = prefs.isRead(_eventId(b));
      if (aRead != bRead) {
        return aRead ? 1 : -1;
      }

      final rankDelta = _priorityRank(a.kind) - _priorityRank(b.kind);
      if (rankDelta != 0) {
        return rankDelta;
      }

      return b.eventAtUtc.compareTo(a.eventAtUtc);
    });

    final visibleFeed = _showUnreadOnly
        ? filteredFeed.where((item) => !prefs.isRead(_eventId(item))).toList()
        : filteredFeed;
    final unreadCount =
        filteredFeed.where((item) => !prefs.isRead(_eventId(item))).length;
    final actionableUnreadCount = filteredFeed
        .where(
          (item) => _isActionable(item.kind) && !prefs.isRead(_eventId(item)),
        )
        .length;

    return RefreshIndicator(
      onRefresh: _loadClientData,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(DmSpacing.xxl),
        children: [
          _buildClientOverviewCard(
            context,
            totalCount: filteredFeed.length,
            unreadCount: unreadCount,
            actionableCount: actionableUnreadCount,
          ),
          const SizedBox(height: 12),
          if (_clientLoading) ...[
            DmCard(
              backgroundColor:
                  isDark ? DmColors.infoSurfaceDark : DmColors.infoSurfaceLight,
              borderColor: isDark ? DmColors.borderDark : DmColors.borderLight,
              child: Row(
                children: [
                  _clientFeedIcon(context, Icons.sync),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          t(context, 'Synchronisation en cours...',
                              'Sync in progress...'),
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          t(
                              context,
                              'Mise a jour des evenements commande/paiement.',
                              'Updating order/payment events.'),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
          ],
          if (_clientError != null) ...[
            DmCard(
              child: Text(
                _clientError!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
            const SizedBox(height: 12),
          ],
          _buildSyncStatusCard(
            context,
            isAdmin: false,
            lastLoadedAt: _clientLastLoadedAt,
            onRefresh: _loadClientData,
          ),
          const SizedBox(height: 12),
          if (!prefs.enabled) ...[
            DmCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    t(context, 'Notifications desactivees',
                        'Notifications disabled'),
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    t(
                      context,
                      'Activez-les dans Parametres pour recevoir les evenements commande/paiement.',
                      'Enable them in Settings to receive order/payment events.',
                    ),
                    style: TextStyle(
                      fontSize: 12,
                      color: DmColors.mutedText(isDark),
                    ),
                  ),
                  const SizedBox(height: 10),
                  OutlinedButton(
                    onPressed: () => unawaited(prefs.setEnabled(true)),
                    child: Text(t(context, 'Activer', 'Enable')),
                  ),
                ],
              ),
            ),
          ] else if (visibleFeed.isEmpty) ...[
            DmCard(
              child: Row(
                children: [
                  Container(
                    height: 56,
                    width: 56,
                    decoration: BoxDecoration(
                      color:
                          isDark ? DmColors.iconBgDark : DmColors.iconBgLight,
                      borderRadius: DmRadius.r16,
                    ),
                    child: const Icon(
                      Icons.notifications_none,
                      color: DmColors.doorOrange,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          t(context, 'Aucune notification', 'No notifications'),
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                        const SizedBox(height: 4),
                        Text(
                          t(
                            context,
                            'Vos evenements commande et paiement apparaitront ici.',
                            'Your order and payment events will appear here.',
                          ),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                        const SizedBox(height: 8),
                        OutlinedButton.icon(
                          onPressed: () => unawaited(_loadClientData()),
                          icon: const Icon(Icons.refresh),
                          label: Text(t(context, 'Actualiser', 'Refresh')),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ] else ...[
            DmCard(
              child: Row(
                children: [
                  _clientFeedIcon(context, Icons.notifications_active_outlined),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          t(context, 'Alertes recentes', 'Recent alerts'),
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          t(
                            context,
                            '${filteredFeed.length} notification(s) issue(s) de vos commandes.',
                            '${filteredFeed.length} notification(s) generated from your orders.',
                          ),
                          style: TextStyle(
                            fontSize: 12,
                            color: DmColors.mutedText(isDark),
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (unreadCount > 0)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: isDark
                            ? DmColors.infoSurfaceDark
                            : const Color(0xFFE8F2FF),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        t(context, '$unreadCount non lus',
                            '$unreadCount unread'),
                        style: TextStyle(
                          color: isDark
                              ? DmColors.textPrimaryDark
                              : const Color(0xFF175CD3),
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            DmCard(
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  FilterChip(
                    selected: _showUnreadOnly,
                    label:
                        Text(t(context, 'Non lus uniquement', 'Unread only')),
                    onSelected: (selected) =>
                        setState(() => _showUnreadOnly = selected),
                  ),
                  ActionChip(
                    label: Text(t(context, 'Tout marquer lu', 'Mark all read')),
                    onPressed: unreadCount == 0
                        ? null
                        : () => unawaited(
                              prefs.markManyRead(
                                filteredFeed.map(_eventId),
                              ),
                            ),
                  ),
                  FilterChip(
                    selected: _showActionableOnly,
                    label: Text(
                        t(context, 'Actions seulement', 'Actionable only')),
                    onSelected: (selected) =>
                        setState(() => _showActionableOnly = selected),
                  ),
                  FilterChip(
                    selected: _categoryFilter == 'all',
                    label: Text(t(context, 'Tous', 'All')),
                    onSelected: (_) => setState(() => _categoryFilter = 'all'),
                  ),
                  FilterChip(
                    selected: _categoryFilter == 'payment',
                    label: Text(t(context, 'Paiements', 'Payments')),
                    onSelected: (_) =>
                        setState(() => _categoryFilter = 'payment'),
                  ),
                  FilterChip(
                    selected: _categoryFilter == 'delivery',
                    label: Text(t(context, 'Livraison', 'Delivery')),
                    onSelected: (_) =>
                        setState(() => _categoryFilter = 'delivery'),
                  ),
                  FilterChip(
                    selected: _categoryFilter == 'order',
                    label: Text(t(context, 'Commandes', 'Orders')),
                    onSelected: (_) =>
                        setState(() => _categoryFilter = 'order'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            for (final item in visibleFeed)
              _buildClientFeedCard(context, item, prefs),
          ],
          const SizedBox(height: 16),
          DmCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  t(context, 'Conseil', 'Tip'),
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 6),
                Text(
                  t(
                    context,
                    'Tirez vers le bas pour actualiser les notifications de commandes.',
                    'Pull down to refresh order notifications.',
                  ),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTypeFilter(BuildContext context) {
    final t = _tr;
    final values = const <String>[
      '',
      'ClientOrderCreated',
      'ClientPaymentPaid',
      'ClientPaymentFailed',
      'ShopOrderPaidPending',
      'AdminOrderPaid',
    ];

    return SizedBox(
      width: 220,
      child: DropdownButtonFormField<String>(
        initialValue: _typeFilter,
        decoration: InputDecoration(
          labelText: t(context, 'Type', 'Type'),
          border: const OutlineInputBorder(),
        ),
        items: values
            .map(
              (value) => DropdownMenuItem<String>(
                value: value,
                child: Text(value.isEmpty ? t(context, 'Tous', 'All') : value),
              ),
            )
            .toList(),
        onChanged: (value) => setState(() => _typeFilter = value ?? ''),
      ),
    );
  }

  Widget _buildStatusFilter(BuildContext context) {
    final t = _tr;
    final values = const <String>['', 'Sent', 'Failed'];
    return SizedBox(
      width: 180,
      child: DropdownButtonFormField<String>(
        initialValue: _statusFilter,
        decoration: InputDecoration(
          labelText: t(context, 'Statut', 'Status'),
          border: const OutlineInputBorder(),
        ),
        items: values
            .map(
              (value) => DropdownMenuItem<String>(
                value: value,
                child: Text(value.isEmpty ? t(context, 'Tous', 'All') : value),
              ),
            )
            .toList(),
        onChanged: (value) => setState(() => _statusFilter = value ?? ''),
      ),
    );
  }

  Widget _buildClientOverviewCard(
    BuildContext context, {
    required int totalCount,
    required int unreadCount,
    required int actionableCount,
  }) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return DmCard(
      backgroundColor:
          isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight,
      borderColor: isDark ? DmColors.borderDark : DmColors.borderLight,
      boxShadowOverride: const [],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t(context, 'Apercu notifications', 'Notifications overview'),
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: _overviewMetric(
                  context,
                  label: t(context, 'Total', 'Total'),
                  value: totalCount,
                  icon: Icons.notifications_outlined,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _overviewMetric(
                  context,
                  label: t(context, 'Non lus', 'Unread'),
                  value: unreadCount,
                  icon: Icons.mark_email_unread_outlined,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _overviewMetric(
                  context,
                  label: t(context, 'Action', 'Action'),
                  value: actionableCount,
                  icon: Icons.priority_high,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _overviewMetric(
    BuildContext context, {
    required String label,
    required int value,
    required IconData icon,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
      decoration: BoxDecoration(
        color: isDark ? DmColors.surfaceDark : DmColors.surfaceLight,
        borderRadius: DmRadius.r12,
        border: Border.all(color: DmColors.border(isDark)),
      ),
      child: Column(
        children: [
          Icon(
            icon,
            size: 16,
            color: DmColors.iconFg(isDark),
          ),
          const SizedBox(height: 4),
          Text(
            value.toString(),
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: TextStyle(
              fontSize: 11,
              color: isDark ? DmColors.textMutedDark : DmColors.textMutedLight,
            ),
          ),
        ],
      ),
    );
  }

  Widget _statusBadge(BuildContext context, String status) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final normalized = status.trim().toLowerCase();
    final isFailed = normalized == 'failed';
    final bg =
        isFailed ? DmColors.errorSurface(isDark) : DmColors.successSurface(isDark);
    final fg = isFailed
        ? (isDark ? DmColors.errorDark : const Color(0xFFB42318))
        : (isDark ? DmColors.successDark : const Color(0xFF067647));
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(status,
          style:
              TextStyle(color: fg, fontWeight: FontWeight.w600, fontSize: 12)),
    );
  }

  Widget _buildSyncStatusCard(
    BuildContext context, {
    required bool isAdmin,
    required DateTime? lastLoadedAt,
    required Future<void> Function() onRefresh,
  }) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final now = DateTime.now();
    final isLoading = isAdmin ? _loading || _busy : _clientLoading || _busy;
    final stale =
        lastLoadedAt == null || now.difference(lastLoadedAt).inMinutes >= 2;
    final statusText = lastLoadedAt == null
        ? t(context, 'Aucune synchro', 'No sync yet')
        : '${t(context, 'Derniere synchro', 'Last sync')}: ${_fmtDate(lastLoadedAt)}';

    return DmCard(
      backgroundColor: stale
          ? DmColors.warningSurface(isDark)
          : DmColors.infoSurface(isDark),
      borderColor: DmColors.border(isDark),
      boxShadowOverride: const [],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _clientFeedIcon(
                context,
                stale ? Icons.sync_problem_outlined : Icons.sync_outlined,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      stale
                          ? t(context, 'Synchro potentiellement obsolete',
                              'Potentially stale sync')
                          : t(context, 'Synchro en direct',
                              'Live synchronization'),
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: isDark
                            ? DmColors.textPrimaryDark
                            : DmColors.textPrimaryLight,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '$statusText - ${t(context, 'Auto: 45s', 'Auto: 45s')}',
                      style: TextStyle(
                        fontSize: 12,
                        color: isDark
                            ? DmColors.textMutedDark
                            : DmColors.textMutedLight,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: 170,
            child: OutlinedButton.icon(
              onPressed: isLoading ? null : () => unawaited(onRefresh()),
              icon: const Icon(Icons.refresh),
              label: Text(t(context, 'Actualiser', 'Refresh')),
            ),
          ),
        ],
      ),
    );
  }

  Widget _clientFeedIcon(BuildContext context, IconData icon) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 42,
      width: 42,
      decoration: BoxDecoration(
        color: isDark ? DmColors.iconBgDark : DmColors.iconBgLight,
        borderRadius: DmRadius.r12,
      ),
      child: Icon(
        icon,
        color: DmColors.iconFg(isDark),
        size: 20,
      ),
    );
  }

  Widget _buildClientFeedCard(
    BuildContext context,
    _ClientNotificationItem item,
    NotificationPreferencesController prefs,
  ) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final title = _clientTitle(context, item);
    final message = _clientMessage(context, item);
    final orderCode = _shortOrderCode(item.order.id);
    final read = prefs.isRead(_eventId(item));

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: DmCard(
        backgroundColor: read
            ? (isDark ? DmColors.surfaceDark : DmColors.surfaceLight)
            : (isDark ? DmColors.infoSurfaceDark : DmColors.infoSurfaceLight),
        borderColor: read
            ? DmColors.border(isDark)
            : (isDark
                ? DmColors.textPrimaryDark.withAlpha(90)
                : const Color(0xFF9CC2FF)),
        boxShadowOverride: const [],
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                _clientFeedIcon(context, _clientIcon(item.kind)),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title,
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 2),
                      Text(
                        '${t(context, 'Commande', 'Order')} $orderCode - ${_fmtDate(item.eventAtUtc)}',
                        style: TextStyle(
                          fontSize: 12,
                          color: DmColors.mutedText(isDark),
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        read
                            ? t(context, 'Lu', 'Read')
                            : t(context, 'Nouveau', 'New'),
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: read
                              ? DmColors.mutedText(isDark)
                              : (isDark
                                  ? DmColors.textPrimaryDark
                                  : const Color(0xFF175CD3)),
                        ),
                      ),
                    ],
                  ),
                ),
                _clientSeverityBadge(context, item.kind),
              ],
            ),
            const SizedBox(height: 10),
            Text(message, style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                OutlinedButton(
                  onPressed: () {
                    unawaited(prefs.markRead(_eventId(item)));
                    context.push('/orders/${item.order.id}');
                  },
                  child: Text(_clientPrimaryActionLabel(context, item.kind)),
                ),
                if (item.kind == ClientNotificationKind.paymentFailed ||
                    item.kind == ClientNotificationKind.paymentPending)
                  OutlinedButton.icon(
                    onPressed: () {
                      unawaited(prefs.markRead(_eventId(item)));
                      _openSupportWithPrefillForOrder(context, item.order);
                    },
                    icon: const Icon(Icons.support_agent),
                    label: Text(t(context, 'Support', 'Support')),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _clientSeverityBadge(
      BuildContext context, ClientNotificationKind kind) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final (bg, fg, text) = switch (kind) {
      ClientNotificationKind.paymentFailed => (
          DmColors.errorSurface(isDark),
          isDark ? DmColors.errorDark : const Color(0xFFB42318),
          _tr(context, 'Critique', 'Critical')
        ),
      ClientNotificationKind.paymentPending => (
          DmColors.warningSurface(isDark),
          isDark ? DmColors.warningDark : const Color(0xFFB54708),
          _tr(context, 'Action', 'Action')
        ),
      ClientNotificationKind.delivered => (
          DmColors.successSurface(isDark),
          isDark ? DmColors.successDark : const Color(0xFF067647),
          _tr(context, 'Livre', 'Delivered')
        ),
      ClientNotificationKind.paymentPaid => (
          DmColors.infoSurface(isDark),
          isDark ? DmColors.textPrimaryDark : const Color(0xFF175CD3),
          _tr(context, 'Paye', 'Paid')
        ),
      ClientNotificationKind.inProgress => (
          DmColors.altSurface(isDark),
          isDark ? DmColors.textPrimaryDark : const Color(0xFF344054),
          _tr(context, 'Suivi', 'Tracking')
        ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(text,
          style:
              TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w700)),
    );
  }

  Future<void> _loadClientData() async {
    if (!mounted) return;
    setState(() {
      _clientLoading = true;
      _clientError = null;
    });

    final cache = ref.read(notificationFeedCacheProvider);
    try {
      if (_clientFeed.isEmpty) {
        try {
          final cachedOrders = await cache.loadCachedOrders();
          final cachedSync = await cache.loadCachedSyncUtc();
          if (cachedOrders.isNotEmpty && mounted) {
            setState(() {
              _clientFeed = _buildClientFeed(cachedOrders);
              _clientLastLoadedAt = cachedSync ?? DateTime.now().toUtc();
            });
          } else if (cachedSync != null &&
              mounted &&
              _clientLastLoadedAt == null) {
            setState(() => _clientLastLoadedAt = cachedSync);
          }
        } catch (_) {
          // Ignore cache read issues; continue with live API fetch.
        }
      }

      final api = ref.read(ordersApiProvider);
      final page = await api.getMine(page: 1, pageSize: 25);
      final feed = _buildClientFeed(page.items);
      await cache.saveCachedOrders(page.items);
      await cache.saveCachedSyncUtc(DateTime.now().toUtc());
      if (!mounted) return;
      setState(() {
        _clientFeed = feed;
        _clientLastLoadedAt = DateTime.now().toUtc();
      });
    } catch (ex) {
      if (!mounted) return;
      setState(() {
        _clientError = _clientFeed.isNotEmpty
            ? _tr(
                context,
                'Reseau indisponible, affichage du cache local.',
                'Network unavailable, showing cached data.',
              )
            : ex.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _clientLoading = false);
      }
    }
  }

  List<_ClientNotificationItem> _buildClientFeed(List<OrderDto> orders) {
    final sorted = [...orders]
      ..sort((a, b) => b.createdAtUtc.compareTo(a.createdAtUtc));
    return sorted
        .take(25)
        .map((order) => _ClientNotificationItem(
              order: order,
              kind: _resolveClientNotificationKind(order),
              eventAtUtc: order.createdAtUtc,
            ))
        .toList();
  }

  ClientNotificationKind _resolveClientNotificationKind(OrderDto order) {
    return resolveClientNotificationKind(order);
  }

  IconData _clientIcon(ClientNotificationKind kind) {
    return switch (kind) {
      ClientNotificationKind.paymentFailed => Icons.error_outline,
      ClientNotificationKind.paymentPending => Icons.pending_actions_outlined,
      ClientNotificationKind.delivered => Icons.local_shipping_outlined,
      ClientNotificationKind.paymentPaid => Icons.check_circle_outline,
      ClientNotificationKind.inProgress => Icons.inventory_2_outlined,
    };
  }

  String _clientTitle(BuildContext context, _ClientNotificationItem item) {
    final t = _tr;
    return switch (item.kind) {
      ClientNotificationKind.paymentFailed =>
        t(context, 'Paiement echoue', 'Payment failed'),
      ClientNotificationKind.paymentPending =>
        t(context, 'Paiement en attente', 'Payment pending'),
      ClientNotificationKind.delivered =>
        t(context, 'Commande livree', 'Order delivered'),
      ClientNotificationKind.paymentPaid =>
        t(context, 'Paiement confirme', 'Payment confirmed'),
      ClientNotificationKind.inProgress =>
        t(context, 'Commande en cours', 'Order in progress'),
    };
  }

  String _clientMessage(BuildContext context, _ClientNotificationItem item) {
    final t = _tr;
    final order = item.order;
    final amount =
        '${order.totalAmount.toStringAsFixed(2)} ${order.currency.toUpperCase()}';
    return switch (item.kind) {
      ClientNotificationKind.paymentFailed => t(
          context,
          'Le paiement de $amount a echoue. Ouvrez la commande pour reprendre rapidement.',
          'Payment of $amount failed. Open the order to resume quickly.',
        ),
      ClientNotificationKind.paymentPending => t(
          context,
          'Le paiement de $amount est en attente. Reprenez la confirmation.',
          'Payment of $amount is pending. Resume confirmation.',
        ),
      ClientNotificationKind.delivered => t(
          context,
          'Votre commande a ete marquee livree.',
          'Your order has been marked as delivered.',
        ),
      ClientNotificationKind.paymentPaid => t(
          context,
          'Paiement valide. La preparation/livraison se poursuit.',
          'Payment validated. Preparation/delivery is continuing.',
        ),
      ClientNotificationKind.inProgress => t(
          context,
          'Suivez la progression de votre commande depuis le detail.',
          'Track your order progress from details.',
        ),
    };
  }

  String _clientPrimaryActionLabel(
      BuildContext context, ClientNotificationKind kind) {
    final t = _tr;
    return switch (kind) {
      ClientNotificationKind.paymentFailed =>
        t(context, 'Reprendre paiement', 'Resume payment'),
      ClientNotificationKind.paymentPending =>
        t(context, 'Continuer paiement', 'Continue payment'),
      _ => t(context, 'Voir commande', 'View order'),
    };
  }

  String _eventId(_ClientNotificationItem item) {
    return buildClientNotificationEventId(item.order, item.kind);
  }

  String _kindKey(ClientNotificationKind kind) {
    return clientNotificationKindKey(kind);
  }

  bool _matchesCategory(ClientNotificationKind kind, String category) {
    switch (category) {
      case 'payment':
        return kind == ClientNotificationKind.paymentFailed ||
            kind == ClientNotificationKind.paymentPending ||
            kind == ClientNotificationKind.paymentPaid;
      case 'delivery':
        return kind == ClientNotificationKind.delivered;
      case 'order':
        return kind == ClientNotificationKind.inProgress;
      case 'all':
      default:
        return true;
    }
  }

  bool _isActionable(ClientNotificationKind kind) {
    return kind == ClientNotificationKind.paymentFailed ||
        kind == ClientNotificationKind.paymentPending;
  }

  int _priorityRank(ClientNotificationKind kind) {
    return switch (kind) {
      ClientNotificationKind.paymentFailed => 0,
      ClientNotificationKind.paymentPending => 1,
      ClientNotificationKind.inProgress => 2,
      ClientNotificationKind.paymentPaid => 3,
      ClientNotificationKind.delivered => 4,
    };
  }

  String _shortOrderCode(String orderId) {
    if (orderId.length <= 8) {
      return orderId.toUpperCase();
    }
    return orderId.substring(0, 8).toUpperCase();
  }

  void _openSupportWithPrefillForOrder(BuildContext context, OrderDto order) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final orderCode = _shortOrderCode(order.id);
    final status = order.paymentStatus.trim().isEmpty
        ? (isFr ? 'INCONNU' : 'UNKNOWN')
        : order.paymentStatus.trim().toUpperCase();
    final provider = order.paymentProvider.trim().isEmpty
        ? 'PayPal'
        : order.paymentProvider.trim();
    final subject = isFr
        ? 'Incident paiement commande $orderCode'
        : 'Payment issue for order $orderCode';
    final message = isFr
        ? 'Bonjour, j ai besoin d aide pour reprendre un paiement.\nCommande: $orderCode\nStatut paiement: $status\nFournisseur: $provider'
        : 'Hello, I need help to resume a payment.\nOrder: $orderCode\nPayment status: $status\nProvider: $provider';

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

  Future<void> _loadAdminData() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final api = ref.read(adminNotificationsApiProvider);

    try {
      final results = await Future.wait<dynamic>([
        api.getSummary(from: _fromUtc, to: _toUtc),
        api.getIncidents(
          type: _typeFilter.isEmpty ? null : _typeFilter,
          from: _fromUtc,
          to: _toUtc,
          minFailures: _minFailures,
          includeAcknowledged: _includeAcknowledged,
          take: _incidentsTake,
        ),
        api.getTransactions(
          type: _typeFilter.isEmpty ? null : _typeFilter,
          status: _statusFilter.isEmpty ? null : _statusFilter,
          from: _fromUtc,
          to: _toUtc,
          page: _page,
          pageSize: _pageSize,
        ),
      ]);

      final page = results[2] as TransactionNotificationsPage;
      setState(() {
        _summary = results[0] as List<TransactionNotificationSummaryDto>;
        _incidents = results[1] as List<NotificationIncidentDto>;
        _rows = page.items;
        _total = page.total;
        _adminLastLoadedAt = DateTime.now().toUtc();
      });
    } catch (ex) {
      setState(() => _error = ex.toString());
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  Future<void> _retryOne(String id) async {
    setState(() => _busy = true);
    try {
      final api = ref.read(adminNotificationsApiProvider);
      final result = await api.retryOne(id);
      _snack('${result.outcome}: ${result.message}');
      await _loadAdminData();
    } catch (ex) {
      _snack(ex.toString(), error: true);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _retryBulk() async {
    setState(() => _busy = true);
    try {
      final api = ref.read(adminNotificationsApiProvider);
      final result = await api.retryFailed(
        limit: 25,
        type: _typeFilter.isEmpty ? null : _typeFilter,
      );
      _snack(
          'Candidates=${result.candidates}, Triggered=${result.triggered}, Failed=${result.failed}');
      await _loadAdminData();
    } catch (ex) {
      _snack(ex.toString(), error: true);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _acknowledgeIncident(String lastLogId) async {
    setState(() => _busy = true);
    try {
      final api = ref.read(adminNotificationsApiProvider);
      await api.acknowledgeIncident(lastLogId);
      if (!mounted) return;
      _snack(_tr(context, 'Incident acquitte', 'Incident acknowledged'));
      await _loadAdminData();
    } catch (ex) {
      _snack(ex.toString(), error: true);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _reopenIncident(String lastLogId) async {
    setState(() => _busy = true);
    try {
      final api = ref.read(adminNotificationsApiProvider);
      await api.reopenIncident(lastLogId);
      if (!mounted) return;
      _snack(_tr(context, 'Incident reouvert', 'Incident reopened'));
      await _loadAdminData();
    } catch (ex) {
      _snack(ex.toString(), error: true);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  void _snack(String message, {bool error = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: error ? Theme.of(context).colorScheme.error : null,
      ),
    );
  }

  static bool _isAdminRole(String? role) {
    final normalized = (role ?? '').trim().toLowerCase();
    return normalized == 'admin' ||
        normalized == 'superadmin' ||
        normalized == '3' ||
        normalized == '4';
  }

  static String _fmtDate(DateTime value) {
    final local = value.toLocal();
    final month = local.month.toString().padLeft(2, '0');
    final day = local.day.toString().padLeft(2, '0');
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');
    return '${local.year}-$month-$day $hour:$minute';
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }
}

class _ClientNotificationItem {
  final OrderDto order;
  final ClientNotificationKind kind;
  final DateTime eventAtUtc;

  const _ClientNotificationItem({
    required this.order,
    required this.kind,
    required this.eventAtUtc,
  });
}

