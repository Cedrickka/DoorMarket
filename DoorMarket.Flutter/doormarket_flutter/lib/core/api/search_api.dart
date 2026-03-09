import '../models/common.dart';
import '../models/products.dart';
import '../models/search.dart';
import '../models/shops.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class SearchApi {
  SearchApi(this._client);

  final ApiClient _client;

  Future<List<SearchSuggestionDto>> suggestions(
    String q, {
    int limit = 8,
  }) async {
    final path = QueryParams.build('api/search/suggestions', {
      'q': q,
      'limit': limit.toString(),
    });

    final response = await _client.get(path);
    final list = response.data as List<dynamic>? ?? const <dynamic>[];
    return list
        .map((item) =>
            SearchSuggestionDto.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  Future<PagedResult<ProductDto>> products(SearchProductsQuery query) async {
    final path = QueryParams.build('api/search/products', {
      'q': query.q,
      'page': query.page.toString(),
      'pageSize': query.pageSize.toString(),
      'inStockOnly': query.inStockOnly.toString().toLowerCase(),
      'promotedOnly': query.promotedOnly.toString().toLowerCase(),
      'minPrice': query.minPrice?.toString(),
      'maxPrice': query.maxPrice?.toString(),
      'ratingMin': query.ratingMin?.toString(),
      'shopId': query.shopId,
      'categoryId': query.categoryId,
      'city': query.city,
      'countryTag': query.countryTag,
      'sort': query.sort,
    });

    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ProductDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<PagedResult<ShopDto>> shops(SearchShopsQuery query) async {
    final path = QueryParams.build('api/search/shops', {
      'q': query.q,
      'page': query.page.toString(),
      'pageSize': query.pageSize.toString(),
      'verifiedOnly': query.verifiedOnly.toString().toLowerCase(),
      'recommended': query.recommended.toString().toLowerCase(),
      'ratingMin': query.ratingMin?.toString(),
      'categoryId': query.categoryId,
      'city': query.city,
      'countryTag': query.countryTag,
      'sort': query.sort,
    });

    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ShopDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<List<SearchCategoryDto>> categories({
    String? q,
    String? countryTag,
    int limit = 24,
  }) async {
    final path = QueryParams.build('api/search/categories', {
      'q': q,
      'countryTag': countryTag,
      'limit': limit.toString(),
    });

    final response = await _client.get(path);
    final list = response.data as List<dynamic>? ?? const <dynamic>[];
    return list
        .map((item) => SearchCategoryDto.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  Future<void> trackQuery(SearchAnalyticsQueryEvent event) async {
    await _client.post('api/search/analytics/query', event.toJson());
  }

  Future<void> trackClick(SearchAnalyticsClickEvent event) async {
    await _client.post('api/search/analytics/click', event.toJson());
  }
}
