class PayPalCheckoutResult {
  final String url;
  final String checkoutId;

  PayPalCheckoutResult({required this.url, required this.checkoutId});

  factory PayPalCheckoutResult.fromJson(Map<String, dynamic> json) {
    return PayPalCheckoutResult(
      url: json['url'] as String,
      checkoutId: json['checkoutId'] as String? ??
          json['payPalOrderId'] as String? ??
          '',
    );
  }
}

class StripeCheckoutResult {
  final String url;
  final String sessionId;

  StripeCheckoutResult({required this.url, required this.sessionId});

  factory StripeCheckoutResult.fromJson(Map<String, dynamic> json) {
    return StripeCheckoutResult(
      url: json['url'] as String,
      sessionId: json['sessionId'] as String? ?? '',
    );
  }
}

class PrepaidPaymentResult {
  final bool paid;
  final String orderId;
  final String paymentStatus;
  final String message;

  PrepaidPaymentResult({
    required this.paid,
    required this.orderId,
    required this.paymentStatus,
    required this.message,
  });

  factory PrepaidPaymentResult.fromJson(Map<String, dynamic> json) {
    return PrepaidPaymentResult(
      paid: json['paid'] as bool? ?? false,
      orderId: json['orderId'] as String? ?? '',
      paymentStatus: json['paymentStatus'] as String? ?? '',
      message: json['message'] as String? ?? '',
    );
  }
}

class MobileMoneyInitiateResult {
  final String provider;
  final String transactionId;
  final String status;
  final String? checkoutUrl;
  final String? message;

  MobileMoneyInitiateResult({
    required this.provider,
    required this.transactionId,
    required this.status,
    this.checkoutUrl,
    this.message,
  });

  factory MobileMoneyInitiateResult.fromJson(Map<String, dynamic> json) {
    return MobileMoneyInitiateResult(
      provider: json['provider'] as String? ?? '',
      transactionId: json['transactionId'] as String? ?? '',
      status: json['status'] as String? ?? '',
      checkoutUrl: json['checkoutUrl'] as String?,
      message: json['message'] as String?,
    );
  }
}

class MobileMoneyConfirmResult {
  final bool paid;
  final String orderId;
  final String paymentStatus;
  final String provider;
  final String transactionId;
  final String? rawStatus;
  final String? message;

  MobileMoneyConfirmResult({
    required this.paid,
    required this.orderId,
    required this.paymentStatus,
    required this.provider,
    required this.transactionId,
    this.rawStatus,
    this.message,
  });

  factory MobileMoneyConfirmResult.fromJson(Map<String, dynamic> json) {
    return MobileMoneyConfirmResult(
      paid: json['paid'] as bool? ?? false,
      orderId: json['orderId'] as String? ?? '',
      paymentStatus: json['paymentStatus'] as String? ?? '',
      provider: json['provider'] as String? ?? '',
      transactionId: json['transactionId'] as String? ?? '',
      rawStatus: json['rawStatus'] as String?,
      message: json['message'] as String?,
    );
  }
}
