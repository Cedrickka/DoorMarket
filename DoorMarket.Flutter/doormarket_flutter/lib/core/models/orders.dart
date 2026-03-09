class OrderDto {
  final String id;
  final String status;
  final String paymentStatus;
  final String fulfillmentStatus;
  final String paymentProvider;
  final double subtotal;
  final double deliveryFee;
  final double discount;
  final double totalAmount;
  final String currency;
  final DateTime createdAtUtc;
  final PaymentMethodSnapshotDto? paymentMethodSnapshot;
  final List<OrderItemDto> items;

  OrderDto({
    required this.id,
    required this.status,
    required this.paymentStatus,
    required this.fulfillmentStatus,
    required this.paymentProvider,
    required this.subtotal,
    required this.deliveryFee,
    required this.discount,
    required this.totalAmount,
    required this.currency,
    required this.createdAtUtc,
    required this.paymentMethodSnapshot,
    required this.items,
  });

  factory OrderDto.fromJson(Map<String, dynamic> json) {
    final itemsJson = (json['items'] as List<dynamic>? ?? []);
    return OrderDto(
      id: json['id'] as String,
      status: json['status'] as String? ?? '',
      paymentStatus: json['paymentStatus'] as String? ?? '',
      fulfillmentStatus: json['fulfillmentStatus'] as String? ?? '',
      paymentProvider: json['paymentProvider'] as String? ?? '',
      subtotal: (json['subtotal'] as num).toDouble(),
      deliveryFee: (json['deliveryFee'] as num).toDouble(),
      discount: (json['discount'] as num).toDouble(),
      totalAmount: (json['totalAmount'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'USD',
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      paymentMethodSnapshot: json['paymentMethodSnapshot'] != null
          ? PaymentMethodSnapshotDto.fromJson(
              json['paymentMethodSnapshot'] as Map<String, dynamic>)
          : null,
      items: itemsJson
          .map((e) => OrderItemDto.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'status': status,
      'paymentStatus': paymentStatus,
      'fulfillmentStatus': fulfillmentStatus,
      'paymentProvider': paymentProvider,
      'subtotal': subtotal,
      'deliveryFee': deliveryFee,
      'discount': discount,
      'totalAmount': totalAmount,
      'currency': currency,
      'createdAtUtc': createdAtUtc.toUtc().toIso8601String(),
      'paymentMethodSnapshot': paymentMethodSnapshot?.toJson(),
      'items': items.map((x) => x.toJson()).toList(),
    };
  }
}

class OrderItemDto {
  final String productId;
  final String productName;
  final int qty;
  final double unitPrice;
  final double lineTotal;
  final String currency;

  OrderItemDto({
    required this.productId,
    required this.productName,
    required this.qty,
    required this.unitPrice,
    required this.lineTotal,
    required this.currency,
  });

  factory OrderItemDto.fromJson(Map<String, dynamic> json) {
    return OrderItemDto(
      productId: json['productId'] as String,
      productName: json['productName'] as String,
      qty: json['qty'] as int,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      lineTotal: (json['lineTotal'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'USD',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'productId': productId,
      'productName': productName,
      'qty': qty,
      'unitPrice': unitPrice,
      'lineTotal': lineTotal,
      'currency': currency,
    };
  }
}

class PaymentMethodSnapshotDto {
  final String provider;
  final String? cardBrand;
  final String? last4;
  final int? expMonth;
  final int? expYear;
  final String? country;
  final String? funding;
  final String? providerPaymentIntentId;
  final String? providerChargeId;

  PaymentMethodSnapshotDto({
    required this.provider,
    this.cardBrand,
    this.last4,
    this.expMonth,
    this.expYear,
    this.country,
    this.funding,
    this.providerPaymentIntentId,
    this.providerChargeId,
  });

  factory PaymentMethodSnapshotDto.fromJson(Map<String, dynamic> json) {
    return PaymentMethodSnapshotDto(
      provider: json['provider'] as String? ?? '',
      cardBrand: json['cardBrand'] as String?,
      last4: json['last4'] as String?,
      expMonth: json['expMonth'] as int?,
      expYear: json['expYear'] as int?,
      country: json['country'] as String?,
      funding: json['funding'] as String?,
      providerPaymentIntentId: json['providerPaymentIntentId'] as String?,
      providerChargeId: json['providerChargeId'] as String?,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'provider': provider,
      'cardBrand': cardBrand,
      'last4': last4,
      'expMonth': expMonth,
      'expYear': expYear,
      'country': country,
      'funding': funding,
      'providerPaymentIntentId': providerPaymentIntentId,
      'providerChargeId': providerChargeId,
    };
  }
}

class CheckoutRequest {
  final String deliveryName;
  final String deliveryPhone;
  final String deliveryLine1;
  final String deliveryCity;
  final String deliveryCountry;
  final String? deliveryZoneId;
  final String? deliveryNotes;
  final String? promoCode;
  final String? paymentProvider;
  final String? prepaidCardCode;

  CheckoutRequest({
    required this.deliveryName,
    required this.deliveryPhone,
    required this.deliveryLine1,
    required this.deliveryCity,
    required this.deliveryCountry,
    this.deliveryZoneId,
    this.deliveryNotes,
    this.promoCode,
    this.paymentProvider,
    this.prepaidCardCode,
  });

  Map<String, dynamic> toJson() => {
        'deliveryName': deliveryName,
        'deliveryPhone': deliveryPhone,
        'deliveryLine1': deliveryLine1,
        'deliveryCity': deliveryCity,
        'deliveryCountry': deliveryCountry,
        'deliveryZoneId': deliveryZoneId,
        'deliveryNotes': deliveryNotes,
        'promoCode': promoCode,
        'paymentProvider': paymentProvider,
        'prepaidCardCode': prepaidCardCode,
      };
}
