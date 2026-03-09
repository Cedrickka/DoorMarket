import '../models/common.dart';
import '../models/products.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class ProductsApi {
  ProductsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<ProductDto>> search(ProductQuery query) async {
    final params = <String, String?>{
      'shopId': query.shopId,
      'categoryId': query.categoryId,
      'countryTag': query.countryTag,
      'city': query.city,
      'q': query.q,
      'minPrice': (query.minPrice ?? 0).toString(),
      'maxPrice': (query.maxPrice ?? 999999999).toString(),
      'inStockOnly': query.inStockOnly.toString().toLowerCase(),
      'activeOnly': query.activeOnly.toString().toLowerCase(),
      'promotedOnly': query.promotedOnly.toString().toLowerCase(),
      'page': query.page.toString(),
      'pageSize': query.pageSize.toString(),
    };

    final path = QueryParams.build('api/products', params);
    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ProductDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<ProductDto> getById(String id) async {
    final response = await _client.get('api/products/$id');
    return ProductDto.fromJson(response.data as Map<String, dynamic>);
  }
}
