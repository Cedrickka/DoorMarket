import '../models/common.dart';
import '../models/orders.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class OrdersApi {
  OrdersApi(this._client);

  final ApiClient _client;

  Future<OrderDto> create(CheckoutRequest request) async {
    final response = await _client.post('api/orders', request.toJson());
    return OrderDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<OrderDto> checkout(CheckoutRequest request) async {
    final response = await _client.post('api/orders/checkout', request.toJson());
    return OrderDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<PagedResult<OrderDto>> getMine({int page = 1, int pageSize = 20}) async {
    final path = QueryParams.build('api/orders/mine', {
      'page': page.toString(),
      'pageSize': pageSize.toString(),
    });
    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => OrderDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<OrderDto> getById(String id) async {
    final response = await _client.get('api/orders/$id');
    return OrderDto.fromJson(response.data as Map<String, dynamic>);
  }
}
