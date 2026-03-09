class CreateReturnRequest {
  final String orderId;
  final String? reasonCode;
  final String reason;
  final String? comment;
  final double? requestedAmount;

  CreateReturnRequest({
    required this.orderId,
    this.reasonCode,
    required this.reason,
    this.comment,
    this.requestedAmount,
  });

  Map<String, dynamic> toJson() => {
        'orderId': orderId,
        'reasonCode': reasonCode,
        'reason': reason,
        'comment': comment,
        'requestedAmount': requestedAmount,
      };
}

class ReturnRequestDto {
  final String id;
  final String orderId;
  final String orderNumber;
  final String status;
  final String? reasonCode;
  final String reason;
  final String? comment;
  final double requestedAmount;
  final double? approvedAmount;
  final String currency;
  final String? adminNote;
  final DateTime createdAtUtc;
  final DateTime? reviewedAtUtc;
  final DateTime? refundedAtUtc;
  final DateTime? slaTargetAtUtc;
  final DateTime? lastStatusChangedAtUtc;
  final bool isSlaBreached;

  ReturnRequestDto({
    required this.id,
    required this.orderId,
    required this.orderNumber,
    required this.status,
    this.reasonCode,
    required this.reason,
    this.comment,
    required this.requestedAmount,
    this.approvedAmount,
    required this.currency,
    this.adminNote,
    required this.createdAtUtc,
    this.reviewedAtUtc,
    this.refundedAtUtc,
    this.slaTargetAtUtc,
    this.lastStatusChangedAtUtc,
    required this.isSlaBreached,
  });

  factory ReturnRequestDto.fromJson(Map<String, dynamic> json) {
    return ReturnRequestDto(
      id: json['id'] as String,
      orderId: json['orderId'] as String,
      orderNumber: json['orderNumber'] as String? ?? '',
      status: json['status'] as String? ?? '',
      reasonCode: json['reasonCode'] as String?,
      reason: json['reason'] as String? ?? '',
      comment: json['comment'] as String?,
      requestedAmount: (json['requestedAmount'] as num?)?.toDouble() ?? 0,
      approvedAmount: (json['approvedAmount'] as num?)?.toDouble(),
      currency: json['currency'] as String? ?? 'USD',
      adminNote: json['adminNote'] as String?,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      reviewedAtUtc: _readDate(json['reviewedAtUtc']),
      refundedAtUtc: _readDate(json['refundedAtUtc']),
      slaTargetAtUtc: _readDate(json['slaTargetAtUtc']),
      lastStatusChangedAtUtc: _readDate(json['lastStatusChangedAtUtc']),
      isSlaBreached: json['isSlaBreached'] as bool? ?? false,
    );
  }

  static DateTime? _readDate(dynamic raw) {
    if (raw is String && raw.isNotEmpty) {
      return DateTime.tryParse(raw);
    }

    return null;
  }
}

class ReturnReasonDto {
  final String code;
  final String titleFr;
  final String titleEn;
  final String? descriptionFr;
  final String? descriptionEn;
  final int defaultSlaHours;
  final int sortOrder;

  const ReturnReasonDto({
    required this.code,
    required this.titleFr,
    required this.titleEn,
    this.descriptionFr,
    this.descriptionEn,
    required this.defaultSlaHours,
    required this.sortOrder,
  });

  factory ReturnReasonDto.fromJson(Map<String, dynamic> json) {
    return ReturnReasonDto(
      code: json['code'] as String? ?? '',
      titleFr: json['titleFr'] as String? ?? '',
      titleEn: json['titleEn'] as String? ?? '',
      descriptionFr: json['descriptionFr'] as String?,
      descriptionEn: json['descriptionEn'] as String?,
      defaultSlaHours: json['defaultSlaHours'] as int? ?? 0,
      sortOrder: json['sortOrder'] as int? ?? 0,
    );
  }
}

class ReturnTimelineEventDto {
  final String id;
  final String oldStatus;
  final String newStatus;
  final String? note;
  final DateTime changedAtUtc;
  final String? changedBy;

  const ReturnTimelineEventDto({
    required this.id,
    required this.oldStatus,
    required this.newStatus,
    this.note,
    required this.changedAtUtc,
    this.changedBy,
  });

  factory ReturnTimelineEventDto.fromJson(Map<String, dynamic> json) {
    return ReturnTimelineEventDto(
      id: json['id'] as String? ?? '',
      oldStatus: json['oldStatus'] as String? ?? '',
      newStatus: json['newStatus'] as String? ?? '',
      note: json['note'] as String?,
      changedAtUtc: DateTime.tryParse(json['changedAtUtc'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      changedBy: json['changedBy'] as String?,
    );
  }
}
