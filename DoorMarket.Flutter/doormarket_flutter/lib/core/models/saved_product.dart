class SavedProductEntry {
  final String productId;
  final String shopId;
  final String shopName;
  final String productName;
  final String? imageUrl;
  final double price;
  final double effectivePrice;
  final bool hasActivePromotion;
  final String currency;
  final DateTime savedAtUtc;

  const SavedProductEntry({
    required this.productId,
    required this.shopId,
    required this.shopName,
    required this.productName,
    this.imageUrl,
    required this.price,
    required this.effectivePrice,
    required this.hasActivePromotion,
    required this.currency,
    required this.savedAtUtc,
  });

  factory SavedProductEntry.fromJson(Map<String, dynamic> json) {
    return SavedProductEntry(
      productId: json['productId'] as String? ?? '',
      shopId: json['shopId'] as String? ?? '',
      shopName: json['shopName'] as String? ?? '',
      productName: json['productName'] as String? ?? '',
      imageUrl: json['imageUrl'] as String?,
      price: (json['price'] as num?)?.toDouble() ?? 0,
      effectivePrice: (json['effectivePrice'] as num?)?.toDouble() ?? 0,
      hasActivePromotion: json['hasActivePromotion'] as bool? ?? false,
      currency: json['currency'] as String? ?? 'USD',
      savedAtUtc: DateTime.tryParse(json['savedAtUtc'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    );
  }

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'shopId': shopId,
        'shopName': shopName,
        'productName': productName,
        'imageUrl': imageUrl,
        'price': price,
        'effectivePrice': effectivePrice,
        'hasActivePromotion': hasActivePromotion,
        'currency': currency,
        'savedAtUtc': savedAtUtc.toUtc().toIso8601String(),
      };
}
