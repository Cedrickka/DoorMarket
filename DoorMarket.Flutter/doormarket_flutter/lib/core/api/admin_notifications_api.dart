import '../models/admin_notifications.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class AdminNotificationsApi {
  AdminNotificationsApi(this._client);

  final ApiClient _client;

  Future<List<TransactionNotificationSummaryDto>> getSummary({
    DateTime? from,
    DateTime? to,
  }) {
    final path =
        QueryParams.build('api/admin/notifications/transactions/summary', {
      'from': _asDate(from),
      'to': _asDate(to),
    });

    return _client.read(
      _client.get(path),
      (data) {
        final items = data is List ? data : const <dynamic>[];
        return items
            .map((item) => TransactionNotificationSummaryDto.fromJson(
                item as Map<String, dynamic>))
            .toList();
      },
    );
  }

  Future<List<NotificationIncidentDto>> getIncidents({
    String? orderId,
    String? type,
    DateTime? from,
    DateTime? to,
    int minFailures = 1,
    bool includeAcknowledged = false,
    int take = 20,
  }) {
    final path =
        QueryParams.build('api/admin/notifications/transactions/incidents', {
      'orderId': orderId,
      'type': type,
      'from': _asDate(from),
      'to': _asDate(to),
      'minFailures': minFailures.toString(),
      'includeAcknowledged': includeAcknowledged.toString(),
      'take': take.toString(),
    });

    return _client.read(
      _client.get(path),
      (data) {
        final items = data is List ? data : const <dynamic>[];
        return items
            .map((item) =>
                NotificationIncidentDto.fromJson(item as Map<String, dynamic>))
            .toList();
      },
    );
  }

  Future<TransactionNotificationsPage> getTransactions({
    String? orderId,
    String? type,
    String? status,
    DateTime? from,
    DateTime? to,
    int page = 1,
    int pageSize = 20,
  }) {
    final path = QueryParams.build('api/admin/notifications/transactions', {
      'orderId': orderId,
      'type': type,
      'status': status,
      'from': _asDate(from),
      'to': _asDate(to),
      'page': page.toString(),
      'pageSize': pageSize.toString(),
    });

    return _client.read(
      _client.get(path),
      (data) =>
          TransactionNotificationsPage.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<RetryTransactionDto> retryOne(String notificationLogId) {
    return _client.read(
      _client.post(
          'api/admin/notifications/transactions/$notificationLogId/retry',
          null),
      (data) => RetryTransactionDto.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<BulkRetryTransactionsDto> retryFailed({
    int limit = 25,
    String? type,
  }) {
    final path =
        QueryParams.build('api/admin/notifications/transactions/retry-failed', {
      'limit': limit.toString(),
      'type': type,
    });
    return _client.read(
      _client.post(path, null),
      (data) => BulkRetryTransactionsDto.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<IncidentAcknowledgeDto> acknowledgeIncident(
    String lastLogId, {
    String? note,
  }) {
    return _client.read(
      _client.post(
        'api/admin/notifications/transactions/incidents/$lastLogId/acknowledge',
        {'note': note},
      ),
      (data) => IncidentAcknowledgeDto.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<IncidentAcknowledgeDto> reopenIncident(String lastLogId) {
    return _client.read(
      _client.post(
          'api/admin/notifications/transactions/incidents/$lastLogId/reopen',
          null),
      (data) => IncidentAcknowledgeDto.fromJson(data as Map<String, dynamic>),
    );
  }

  static String? _asDate(DateTime? value) {
    if (value == null) return null;
    final utc = value.toUtc();
    final month = utc.month.toString().padLeft(2, '0');
    final day = utc.day.toString().padLeft(2, '0');
    return '${utc.year}-$month-$day';
  }
}
