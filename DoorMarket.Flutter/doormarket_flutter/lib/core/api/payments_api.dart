import '../models/payments.dart';
import '../network/api_client.dart';

class PaymentsApi {
  PaymentsApi(this._client);

  final ApiClient _client;

  Future<PayPalCheckoutResult> createPayPalCheckout(String orderId) async {
    final response = await _client.post('api/payments/paypal/create-checkout?orderId=$orderId', {});
    return PayPalCheckoutResult.fromJson(response.data as Map<String, dynamic>);
  }

  Future<StripeCheckoutResult> createStripeCheckout(String orderId) async {
    final response = await _client.post('api/payments/stripe/checkout-session?orderId=$orderId', {});
    return StripeCheckoutResult.fromJson(response.data as Map<String, dynamic>);
  }

  Future<PrepaidPaymentResult> payWithPrepaidCard(String orderId, String cardCode) async {
    final response = await _client.post('api/payments/prepaid/pay', {
      'orderId': orderId,
      'cardCode': cardCode,
    });
    return PrepaidPaymentResult.fromJson(response.data as Map<String, dynamic>);
  }

  Future<MobileMoneyInitiateResult> initiateMobileMoney({
    required String orderId,
    required String provider,
    required String phoneNumber,
    String? callbackUrl,
  }) async {
    final response = await _client.post('api/payments/mobile-money/initiate', {
      'orderId': orderId,
      'provider': provider,
      'phoneNumber': phoneNumber,
      'callbackUrl': callbackUrl,
    });
    return MobileMoneyInitiateResult.fromJson(
        response.data as Map<String, dynamic>);
  }

  Future<MobileMoneyConfirmResult> confirmMobileMoney({
    required String orderId,
    required String provider,
    required String transactionId,
  }) async {
    final response = await _client.post('api/payments/mobile-money/confirm', {
      'orderId': orderId,
      'provider': provider,
      'transactionId': transactionId,
    });
    return MobileMoneyConfirmResult.fromJson(
        response.data as Map<String, dynamic>);
  }
}
