class DeliveryZoneDto {
  final String id;
  final String code;
  final String name;
  final String country;
  final String? stateCode;
  final double feeUsd;
  final bool isActive;

  DeliveryZoneDto({
    required this.id,
    required this.code,
    required this.name,
    required this.country,
    this.stateCode,
    required this.feeUsd,
    required this.isActive,
  });

  factory DeliveryZoneDto.fromJson(Map<String, dynamic> json) {
    return DeliveryZoneDto(
      id: json['id'] as String,
      code: json['code'] as String,
      name: json['name'] as String,
      country: json['country'] as String,
      stateCode: json['stateCode'] as String?,
      feeUsd: (json['feeUsd'] as num).toDouble(),
      isActive: json['isActive'] as bool? ?? false,
    );
  }
}
