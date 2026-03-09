class ProductDto {
  final String id;
  final String shopId;
  final String shopName;
  final String categoryId;
  final String categoryName;
  final String? categoryNameEn;
  final String name;
  final String? description;
  final double price;
  final double effectivePrice;
  final bool hasActivePromotion;
  final bool isPromotionEnabled;
  final double? promotionPrice;
  final double? promotionPercent;
  final DateTime? promotionStartUtc;
  final DateTime? promotionEndUtc;
  final String currency;
  final int stockQty;
  final bool isActive;
  final String? mainImageUrl;
  final DateTime createdAtUtc;
  final double? shopRating;
  final int? shopReviewCount;

  ProductDto({
    required this.id,
    required this.shopId,
    required this.shopName,
    required this.categoryId,
    required this.categoryName,
    this.categoryNameEn,
    required this.name,
    this.description,
    required this.price,
    required this.effectivePrice,
    required this.hasActivePromotion,
    required this.isPromotionEnabled,
    this.promotionPrice,
    this.promotionPercent,
    this.promotionStartUtc,
    this.promotionEndUtc,
    required this.currency,
    required this.stockQty,
    required this.isActive,
    this.mainImageUrl,
    required this.createdAtUtc,
    this.shopRating,
    this.shopReviewCount,
  });

  factory ProductDto.fromJson(Map<String, dynamic> json) {
    return ProductDto(
      id: json['id'] as String,
      shopId: json['shopId'] as String,
      shopName: json['shopName'] as String,
      categoryId: json['categoryId'] as String,
      categoryName: json['categoryName'] as String,
      categoryNameEn: json['categoryNameEn'] as String?,
      name: json['name'] as String,
      description: json['description'] as String?,
      price: (json['price'] as num).toDouble(),
      effectivePrice: (json['effectivePrice'] as num).toDouble(),
      hasActivePromotion: json['hasActivePromotion'] as bool? ?? false,
      isPromotionEnabled: json['isPromotionEnabled'] as bool? ?? false,
      promotionPrice: (json['promotionPrice'] as num?)?.toDouble(),
      promotionPercent: (json['promotionPercent'] as num?)?.toDouble(),
      promotionStartUtc: json['promotionStartUtc'] != null
          ? DateTime.parse(json['promotionStartUtc'] as String)
          : null,
      promotionEndUtc: json['promotionEndUtc'] != null
          ? DateTime.parse(json['promotionEndUtc'] as String)
          : null,
      currency: json['currency'] as String? ?? 'USD',
      stockQty: json['stockQty'] as int? ?? 0,
      isActive: json['isActive'] as bool? ?? false,
      mainImageUrl: json['mainImageUrl'] as String?,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      shopRating: (json['shopRating'] as num?)?.toDouble(),
      shopReviewCount: json['shopReviewCount'] as int?,
    );
  }
}

class ProductQuery {
  final String? shopId;
  final String? categoryId;
  final String? countryTag;
  final String? city;
  final String? q;
  final double? minPrice;
  final double? maxPrice;
  final bool inStockOnly;
  final bool activeOnly;
  final bool promotedOnly;
  final int page;
  final int pageSize;

  const ProductQuery({
    this.shopId,
    this.categoryId,
    this.countryTag,
    this.city,
    this.q,
    this.minPrice,
    this.maxPrice,
    this.inStockOnly = false,
    this.activeOnly = true,
    this.promotedOnly = false,
    this.page = 1,
    this.pageSize = 20,
  });
}
