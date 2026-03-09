class CartDto {
  final String cartId;
  final List<CartItemDto> items;
  final double subtotal;
  final String currency;

  CartDto({
    required this.cartId,
    required this.items,
    required this.subtotal,
    required this.currency,
  });

  factory CartDto.fromJson(Map<String, dynamic> json) {
    final itemsJson = (json['items'] as List<dynamic>? ?? []);
    return CartDto(
      cartId: json['cartId'] as String,
      items: itemsJson
          .map((e) => CartItemDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      subtotal: (json['subtotal'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'USD',
    );
  }
}

class CartItemDto {
  final String id;
  final String productId;
  final String? shopId;
  final String productName;
  final double unitPrice;
  final int qty;
  final double lineTotal;
  final String currency;
  final String? mainImageUrl;

  CartItemDto({
    required this.id,
    required this.productId,
    this.shopId,
    required this.productName,
    required this.unitPrice,
    required this.qty,
    required this.lineTotal,
    required this.currency,
    this.mainImageUrl,
  });

  factory CartItemDto.fromJson(Map<String, dynamic> json) {
    return CartItemDto(
      id: json['id'] as String,
      productId: json['productId'] as String,
      shopId: json['shopId'] as String?,
      productName: json['productName'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      qty: json['qty'] as int,
      lineTotal: (json['lineTotal'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'USD',
      mainImageUrl: json['mainImageUrl'] as String?,
    );
  }
}

class PromoQuoteDto {
  final String? promoCode;
  final bool applied;
  final double discount;
  final String message;

  PromoQuoteDto({
    required this.promoCode,
    required this.applied,
    required this.discount,
    required this.message,
  });

  factory PromoQuoteDto.fromJson(Map<String, dynamic> json) {
    return PromoQuoteDto(
      promoCode: json['promoCode'] as String?,
      applied: json['applied'] as bool? ?? false,
      discount: (json['discount'] as num?)?.toDouble() ?? 0,
      message: json['message'] as String? ?? '',
    );
  }
}

class CouponValidationRequest {
  final String code;
  final String? deliveryZoneId;
  final bool? requireDeliveryZone;

  CouponValidationRequest({
    required this.code,
    this.deliveryZoneId,
    this.requireDeliveryZone,
  });

  Map<String, dynamic> toJson() => {
        'code': code,
        'deliveryZoneId': deliveryZoneId,
        'requireDeliveryZone': requireDeliveryZone,
      };
}

class CouponValidationDto {
  final bool isReady;
  final String? promoCode;
  final bool applied;
  final String message;
  final String currency;
  final int itemCount;
  final double subtotal;
  final double discount;
  final double deliveryFee;
  final double totalEstimate;
  final List<CheckoutIssueDto> blockingIssues;
  final List<CheckoutIssueDto> warnings;

  CouponValidationDto({
    required this.isReady,
    required this.promoCode,
    required this.applied,
    required this.message,
    required this.currency,
    required this.itemCount,
    required this.subtotal,
    required this.discount,
    required this.deliveryFee,
    required this.totalEstimate,
    required this.blockingIssues,
    required this.warnings,
  });

  factory CouponValidationDto.fromJson(Map<String, dynamic> json) {
    final blocking = (json['blockingIssues'] as List<dynamic>? ?? [])
        .map((e) => CheckoutIssueDto.fromJson(e as Map<String, dynamic>))
        .toList();
    final warnings = (json['warnings'] as List<dynamic>? ?? [])
        .map((e) => CheckoutIssueDto.fromJson(e as Map<String, dynamic>))
        .toList();

    return CouponValidationDto(
      isReady: json['isReady'] as bool? ?? false,
      promoCode: json['promoCode'] as String?,
      applied: json['applied'] as bool? ?? false,
      message: json['message'] as String? ?? '',
      currency: json['currency'] as String? ?? 'USD',
      itemCount: json['itemCount'] as int? ?? 0,
      subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
      discount: (json['discount'] as num?)?.toDouble() ?? 0,
      deliveryFee: (json['deliveryFee'] as num?)?.toDouble() ?? 0,
      totalEstimate: (json['totalEstimate'] as num?)?.toDouble() ?? 0,
      blockingIssues: blocking,
      warnings: warnings,
    );
  }
}

class DeliveryQuoteDto {
  final double subtotal;
  final double deliveryFee;
  final String currency;

  DeliveryQuoteDto({
    required this.subtotal,
    required this.deliveryFee,
    required this.currency,
  });

  factory DeliveryQuoteDto.fromJson(Map<String, dynamic> json) {
    return DeliveryQuoteDto(
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'USD',
    );
  }
}

class PreCheckoutRequest {
  final String? deliveryZoneId;
  final String? promoCode;
  final String? paymentProvider;
  final String? prepaidCardCode;
  final bool? requireDeliveryZone;

  PreCheckoutRequest({
    this.deliveryZoneId,
    this.promoCode,
    this.paymentProvider,
    this.prepaidCardCode,
    this.requireDeliveryZone,
  });

  Map<String, dynamic> toJson() => {
        'deliveryZoneId': deliveryZoneId,
        'promoCode': promoCode,
        'paymentProvider': paymentProvider,
        'prepaidCardCode': prepaidCardCode,
        'requireDeliveryZone': requireDeliveryZone,
      };
}

class CheckoutIssueDto {
  final String code;
  final String message;
  final String? productName;

  CheckoutIssueDto({
    required this.code,
    required this.message,
    this.productName,
  });

  factory CheckoutIssueDto.fromJson(Map<String, dynamic> json) {
    return CheckoutIssueDto(
      code: json['code'] as String? ?? '',
      message: json['message'] as String? ?? '',
      productName: json['productName'] as String?,
    );
  }
}

class CheckoutReadinessDto {
  final bool isReady;
  final String currency;
  final int itemCount;
  final double subtotal;
  final double discount;
  final double deliveryFee;
  final double totalEstimate;
  final String paymentProvider;
  final List<CheckoutIssueDto> blockingIssues;
  final List<CheckoutIssueDto> warnings;

  CheckoutReadinessDto({
    required this.isReady,
    required this.currency,
    required this.itemCount,
    required this.subtotal,
    required this.discount,
    required this.deliveryFee,
    required this.totalEstimate,
    required this.paymentProvider,
    required this.blockingIssues,
    required this.warnings,
  });

  factory CheckoutReadinessDto.fromJson(Map<String, dynamic> json) {
    final blocking = (json['blockingIssues'] as List<dynamic>? ?? [])
        .map((e) => CheckoutIssueDto.fromJson(e as Map<String, dynamic>))
        .toList();
    final warnings = (json['warnings'] as List<dynamic>? ?? [])
        .map((e) => CheckoutIssueDto.fromJson(e as Map<String, dynamic>))
        .toList();

    return CheckoutReadinessDto(
      isReady: json['isReady'] as bool? ?? false,
      currency: json['currency'] as String? ?? 'USD',
      itemCount: json['itemCount'] as int? ?? 0,
      subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
      discount: (json['discount'] as num?)?.toDouble() ?? 0,
      deliveryFee: (json['deliveryFee'] as num?)?.toDouble() ?? 0,
      totalEstimate: (json['totalEstimate'] as num?)?.toDouble() ?? 0,
      paymentProvider: json['paymentProvider'] as String? ?? 'PayPal',
      blockingIssues: blocking,
      warnings: warnings,
    );
  }
}

class CartRecoveryStatusDto {
  final bool hasActiveReminder;
  final String message;
  final DateTime? lastDetectedAtUtc;
  final String? reminderStatus;
  final String? experimentGroup;
  final int itemCount;
  final double subtotal;
  final String currency;
  final bool hasRecentOrder;
  final String checkoutPath;

  CartRecoveryStatusDto({
    required this.hasActiveReminder,
    required this.message,
    required this.lastDetectedAtUtc,
    required this.reminderStatus,
    required this.experimentGroup,
    required this.itemCount,
    required this.subtotal,
    required this.currency,
    required this.hasRecentOrder,
    required this.checkoutPath,
  });

  factory CartRecoveryStatusDto.fromJson(Map<String, dynamic> json) {
    return CartRecoveryStatusDto(
      hasActiveReminder: json['hasActiveReminder'] as bool? ?? false,
      message: json['message'] as String? ?? '',
      lastDetectedAtUtc: json['lastDetectedAtUtc'] == null
          ? null
          : DateTime.tryParse(json['lastDetectedAtUtc'] as String),
      reminderStatus: json['reminderStatus'] as String?,
      experimentGroup: json['experimentGroup'] as String?,
      itemCount: json['itemCount'] as int? ?? 0,
      subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
      currency: json['currency'] as String? ?? 'USD',
      hasRecentOrder: json['hasRecentOrder'] as bool? ?? false,
      checkoutPath: json['checkoutPath'] as String? ?? '/checkout',
    );
  }
}

class AddCartItemRequest {
  final String productId;
  final int qty;

  AddCartItemRequest({required this.productId, required this.qty});

  Map<String, dynamic> toJson() => {
        'productId': productId,
        'qty': qty,
      };
}

class UpdateCartItemRequest {
  final int qty;

  UpdateCartItemRequest({required this.qty});

  Map<String, dynamic> toJson() => {'qty': qty};
}

class ApplyPromoRequest {
  final String code;

  ApplyPromoRequest({required this.code});

  Map<String, dynamic> toJson() => {'code': code};
}
