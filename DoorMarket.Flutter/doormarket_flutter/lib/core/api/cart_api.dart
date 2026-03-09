import '../models/cart.dart';
import '../network/api_client.dart';

class CartApi {
  CartApi(this._client);

  final ApiClient _client;

  Future<CartDto> getMyCart() async {
    final response = await _client.get('api/cart');
    return CartDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CartDto> addItem(AddCartItemRequest request) async {
    final response = await _client.post('api/cart/items', request.toJson());
    return CartDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CartDto> updateItem(
      String itemId, UpdateCartItemRequest request) async {
    final response =
        await _client.put('api/cart/items/$itemId', request.toJson());
    return CartDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CartDto> removeItem(String itemId) async {
    final response = await _client.delete('api/cart/items/$itemId');
    return CartDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> clear() async {
    await _client.delete('api/cart/clear');
  }

  Future<PromoQuoteDto> applyPromo(ApplyPromoRequest request) async {
    final response =
        await _client.post('api/cart/apply-promo', request.toJson());
    return PromoQuoteDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CouponValidationDto> validateCoupon(
      CouponValidationRequest request) async {
    final response =
        await _client.post('api/cart/validate-coupon', request.toJson());
    return CouponValidationDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CheckoutReadinessDto> preCheckout(PreCheckoutRequest request) async {
    final response =
        await _client.post('api/cart/pre-checkout', request.toJson());
    return CheckoutReadinessDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<CartRecoveryStatusDto> getRecoveryStatus({int lookbackDays = 30}) async {
    final response = await _client
        .get('api/cart/recovery-status?lookbackDays=$lookbackDays');
    return CartRecoveryStatusDto.fromJson(response.data as Map<String, dynamic>);
  }
}
