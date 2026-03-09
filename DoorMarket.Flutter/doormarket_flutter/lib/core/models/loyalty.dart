class LoyaltyWalletDto {
  final String userId;
  final int pointsBalance;
  final int lifetimeEarned;
  final int lifetimeSpent;
  final double walletValue;
  final String walletCurrency;
  final DateTime? lastEarnedAtUtc;

  const LoyaltyWalletDto({
    required this.userId,
    required this.pointsBalance,
    required this.lifetimeEarned,
    required this.lifetimeSpent,
    required this.walletValue,
    required this.walletCurrency,
    this.lastEarnedAtUtc,
  });

  factory LoyaltyWalletDto.fromJson(Map<String, dynamic> json) {
    return LoyaltyWalletDto(
      userId: json['userId'] as String? ?? '',
      pointsBalance: json['pointsBalance'] as int? ?? 0,
      lifetimeEarned: json['lifetimeEarned'] as int? ?? 0,
      lifetimeSpent: json['lifetimeSpent'] as int? ?? 0,
      walletValue: (json['walletValue'] as num?)?.toDouble() ?? 0,
      walletCurrency: json['walletCurrency'] as String? ?? 'USD',
      lastEarnedAtUtc: _readDate(json['lastEarnedAtUtc']),
    );
  }

  static DateTime? _readDate(dynamic raw) {
    if (raw is String && raw.isNotEmpty) {
      return DateTime.tryParse(raw);
    }
    return null;
  }
}

class LoyaltyLedgerEntryDto {
  final String id;
  final String sourceType;
  final String sourceId;
  final int pointsDelta;
  final double walletValueDelta;
  final String walletCurrency;
  final String note;
  final DateTime createdAtUtc;

  const LoyaltyLedgerEntryDto({
    required this.id,
    required this.sourceType,
    required this.sourceId,
    required this.pointsDelta,
    required this.walletValueDelta,
    required this.walletCurrency,
    required this.note,
    required this.createdAtUtc,
  });

  factory LoyaltyLedgerEntryDto.fromJson(Map<String, dynamic> json) {
    return LoyaltyLedgerEntryDto(
      id: json['id'] as String? ?? '',
      sourceType: json['sourceType'] as String? ?? '',
      sourceId: json['sourceId'] as String? ?? '',
      pointsDelta: json['pointsDelta'] as int? ?? 0,
      walletValueDelta: (json['walletValueDelta'] as num?)?.toDouble() ?? 0,
      walletCurrency: json['walletCurrency'] as String? ?? 'USD',
      note: json['note'] as String? ?? '',
      createdAtUtc: DateTime.tryParse(json['createdAtUtc'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    );
  }
}

