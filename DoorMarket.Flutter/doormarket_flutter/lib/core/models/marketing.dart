class MarketingBannerDto {
  final String id;
  final String title;
  final String? subtitle;
  final String? imageUrl;
  final String? targetUrl;
  final bool isActive;
  final DateTime? startAtUtc;
  final DateTime? endAtUtc;
  final String? language;
  final String? city;
  final String? zone;
  final String? categoryId;
  final String? shopId;
  final int sortOrder;
  final int impressions;
  final int clicks;
  final DateTime createdAtUtc;

  MarketingBannerDto({
    required this.id,
    required this.title,
    this.subtitle,
    this.imageUrl,
    this.targetUrl,
    required this.isActive,
    this.startAtUtc,
    this.endAtUtc,
    this.language,
    this.city,
    this.zone,
    this.categoryId,
    this.shopId,
    required this.sortOrder,
    required this.impressions,
    required this.clicks,
    required this.createdAtUtc,
  });

  factory MarketingBannerDto.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(String? value) => value == null || value.isEmpty ? null : DateTime.parse(value);

    return MarketingBannerDto(
      id: json['id'] as String,
      title: json['title'] as String? ?? '',
      subtitle: json['subtitle'] as String?,
      imageUrl: json['imageUrl'] as String?,
      targetUrl: json['targetUrl'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      startAtUtc: parseDate(json['startAtUtc'] as String?),
      endAtUtc: parseDate(json['endAtUtc'] as String?),
      language: json['language'] as String?,
      city: json['city'] as String?,
      zone: json['zone'] as String?,
      categoryId: json['categoryId'] as String?,
      shopId: json['shopId'] as String?,
      sortOrder: json['sortOrder'] as int? ?? 0,
      impressions: json['impressions'] as int? ?? 0,
      clicks: json['clicks'] as int? ?? 0,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }
}
