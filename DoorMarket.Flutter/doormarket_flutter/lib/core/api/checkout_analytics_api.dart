import '../network/api_client.dart';

class CheckoutAnalyticsApi {
  CheckoutAnalyticsApi(this._client);

  final ApiClient _client;

  Future<void> track({
    required String eventName,
    String? orderId,
    String? sessionId,
    String? paymentProvider,
    String? paymentChannel,
    String? experimentName,
    String? experimentGroup,
    bool? success,
    int? durationMs,
    String? errorCode,
    String? errorMessage,
    String? source,
    String? countryTag,
    Map<String, dynamic>? metadata,
  }) async {
    await _client.post('api/checkout/analytics/track', {
      'eventName': eventName,
      'orderId': orderId,
      'sessionId': sessionId,
      'paymentProvider': paymentProvider,
      'paymentChannel': paymentChannel,
      'experimentName': experimentName,
      'experimentGroup': experimentGroup,
      'success': success,
      'durationMs': durationMs,
      'errorCode': errorCode,
      'errorMessage': errorMessage,
      'source': source,
      'countryTag': countryTag,
      'metadata': metadata,
    });
  }
}
