import 'common.dart';

class TransactionNotificationSummaryDto {
  final String notificationType;
  final int total;
  final int sent;
  final int failed;
  final DateTime? lastAttemptUtc;

  TransactionNotificationSummaryDto({
    required this.notificationType,
    required this.total,
    required this.sent,
    required this.failed,
    required this.lastAttemptUtc,
  });

  factory TransactionNotificationSummaryDto.fromJson(
      Map<String, dynamic> json) {
    return TransactionNotificationSummaryDto(
      notificationType: json['notificationType'] as String? ?? '',
      total: json['total'] as int? ?? 0,
      sent: json['sent'] as int? ?? 0,
      failed: json['failed'] as int? ?? 0,
      lastAttemptUtc: _parseDate(json['lastAttemptUtc']),
    );
  }
}

class TransactionNotificationDto {
  final String id;
  final String? orderId;
  final String notificationType;
  final String channel;
  final String recipient;
  final String subject;
  final String status;
  final String? error;
  final DateTime attemptedAtUtc;

  TransactionNotificationDto({
    required this.id,
    required this.orderId,
    required this.notificationType,
    required this.channel,
    required this.recipient,
    required this.subject,
    required this.status,
    required this.error,
    required this.attemptedAtUtc,
  });

  factory TransactionNotificationDto.fromJson(Map<String, dynamic> json) {
    return TransactionNotificationDto(
      id: json['id'] as String? ?? '',
      orderId: json['orderId'] as String?,
      notificationType: json['notificationType'] as String? ?? '',
      channel: json['channel'] as String? ?? '',
      recipient: json['recipient'] as String? ?? '',
      subject: json['subject'] as String? ?? '',
      status: json['status'] as String? ?? '',
      error: json['error'] as String?,
      attemptedAtUtc:
          _parseDate(json['attemptedAtUtc']) ?? DateTime.now().toUtc(),
    );
  }
}

class NotificationIncidentDto {
  final String lastLogId;
  final String? orderId;
  final String notificationType;
  final String recipient;
  final int failedCount;
  final DateTime firstAttemptUtc;
  final DateTime lastAttemptUtc;
  final String? lastError;
  final bool acknowledged;
  final DateTime? acknowledgedAtUtc;
  final String? acknowledgedBy;
  final String? acknowledgementNote;

  NotificationIncidentDto({
    required this.lastLogId,
    required this.orderId,
    required this.notificationType,
    required this.recipient,
    required this.failedCount,
    required this.firstAttemptUtc,
    required this.lastAttemptUtc,
    required this.lastError,
    required this.acknowledged,
    required this.acknowledgedAtUtc,
    required this.acknowledgedBy,
    required this.acknowledgementNote,
  });

  factory NotificationIncidentDto.fromJson(Map<String, dynamic> json) {
    return NotificationIncidentDto(
      lastLogId: json['lastLogId'] as String? ?? '',
      orderId: json['orderId'] as String?,
      notificationType: json['notificationType'] as String? ?? '',
      recipient: json['recipient'] as String? ?? '',
      failedCount: json['failedCount'] as int? ?? 0,
      firstAttemptUtc:
          _parseDate(json['firstAttemptUtc']) ?? DateTime.now().toUtc(),
      lastAttemptUtc:
          _parseDate(json['lastAttemptUtc']) ?? DateTime.now().toUtc(),
      lastError: json['lastError'] as String?,
      acknowledged: json['acknowledged'] as bool? ?? false,
      acknowledgedAtUtc: _parseDate(json['acknowledgedAtUtc']),
      acknowledgedBy: json['acknowledgedBy'] as String?,
      acknowledgementNote: json['acknowledgementNote'] as String?,
    );
  }
}

class RetryTransactionDto {
  final String notificationLogId;
  final bool retried;
  final String outcome;
  final String message;
  final String? orderId;
  final String? notificationType;

  RetryTransactionDto({
    required this.notificationLogId,
    required this.retried,
    required this.outcome,
    required this.message,
    required this.orderId,
    required this.notificationType,
  });

  factory RetryTransactionDto.fromJson(Map<String, dynamic> json) {
    return RetryTransactionDto(
      notificationLogId: json['notificationLogId'] as String? ?? '',
      retried: json['retried'] as bool? ?? false,
      outcome: json['outcome'] as String? ?? '',
      message: json['message'] as String? ?? '',
      orderId: json['orderId'] as String?,
      notificationType: json['notificationType'] as String?,
    );
  }
}

class BulkRetryTransactionsDto {
  final int candidates;
  final int triggered;
  final int ignored;
  final int failed;

  BulkRetryTransactionsDto({
    required this.candidates,
    required this.triggered,
    required this.ignored,
    required this.failed,
  });

  factory BulkRetryTransactionsDto.fromJson(Map<String, dynamic> json) {
    return BulkRetryTransactionsDto(
      candidates: json['candidates'] as int? ?? 0,
      triggered: json['triggered'] as int? ?? 0,
      ignored: json['ignored'] as int? ?? 0,
      failed: json['failed'] as int? ?? 0,
    );
  }
}

class IncidentAcknowledgeDto {
  final String lastLogId;
  final String? orderId;
  final String notificationType;
  final String recipient;
  final bool isActive;
  final DateTime acknowledgedAtUtc;
  final String acknowledgedBy;
  final String? note;

  IncidentAcknowledgeDto({
    required this.lastLogId,
    required this.orderId,
    required this.notificationType,
    required this.recipient,
    required this.isActive,
    required this.acknowledgedAtUtc,
    required this.acknowledgedBy,
    required this.note,
  });

  factory IncidentAcknowledgeDto.fromJson(Map<String, dynamic> json) {
    return IncidentAcknowledgeDto(
      lastLogId: json['lastLogId'] as String? ?? '',
      orderId: json['orderId'] as String?,
      notificationType: json['notificationType'] as String? ?? '',
      recipient: json['recipient'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? false,
      acknowledgedAtUtc:
          _parseDate(json['acknowledgedAtUtc']) ?? DateTime.now().toUtc(),
      acknowledgedBy: json['acknowledgedBy'] as String? ?? '',
      note: json['note'] as String?,
    );
  }
}

class TransactionNotificationsPage
    extends PagedResult<TransactionNotificationDto> {
  TransactionNotificationsPage({
    required super.page,
    required super.pageSize,
    required super.total,
    required super.items,
  });

  factory TransactionNotificationsPage.fromJson(Map<String, dynamic> json) {
    final page = PagedResult.fromJson(
      json,
      (item) =>
          TransactionNotificationDto.fromJson(item as Map<String, dynamic>),
    );
    return TransactionNotificationsPage(
      page: page.page,
      pageSize: page.pageSize,
      total: page.total,
      items: page.items,
    );
  }
}

DateTime? _parseDate(dynamic raw) {
  if (raw == null) return null;
  if (raw is String && raw.isNotEmpty) {
    return DateTime.tryParse(raw);
  }
  return null;
}
