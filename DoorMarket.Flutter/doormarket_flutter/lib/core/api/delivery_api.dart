import '../models/cart.dart';
import '../models/delivery.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class DeliveryApi {
  DeliveryApi(this._client);

  final ApiClient _client;

  Future<DeliveryQuoteDto> getQuote({
    required double subtotal,
    String? currency,
    String? zoneId,
  }) async {
    final path = QueryParams.build('api/delivery/quote', {
      'subtotal': subtotal.toString(),
      'currency': currency,
      'zoneId': zoneId,
    });
    final response = await _client.get(path);
    return DeliveryQuoteDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<List<DeliveryZoneDto>> getZones() async {
    final response = await _client.get('api/delivery/zones');
    final data = response.data as List<dynamic>? ?? [];
    return data.map((e) => DeliveryZoneDto.fromJson(e as Map<String, dynamic>)).toList();
  }
}
