import '../models/common.dart';
import '../models/shops.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class ShopsApi {
  ShopsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<ShopDto>> search({
    required ShopQuery query,
    String? categoryId,
    bool? recommended,
  }) async {
    final params = <String, String?>{
      'countryTag': query.countryTag,
      'city': query.city,
      'q': query.q,
      'page': query.page.toString(),
      'pageSize': query.pageSize.toString(),
      'verifiedOnly': query.verifiedOnly.toString().toLowerCase(),
      'categoryId': categoryId,
      'recommended': recommended?.toString().toLowerCase(),
    };
    final path = QueryParams.build('api/shops', params);
    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ShopDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<ShopDto> getById(String id) async {
    final response = await _client.get('api/shops/$id');
    return ShopDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<ShopReviewSummaryDto> getReviewsSummary(String shopId) async {
    final response = await _client.get('api/shops/$shopId/reviews/summary');
    return ShopReviewSummaryDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<PagedResult<ShopReviewDto>> getReviews(
    String shopId, {
    int page = 1,
    int pageSize = 20,
    String sort = 'recent',
  }) async {
    final path = QueryParams.build(
      'api/shops/$shopId/reviews',
      {
        'page': page.toString(),
        'pageSize': pageSize.toString(),
        'sort': sort,
      },
    );
    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ShopReviewDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<ShopReviewDto> upsertReview(
    String shopId,
    CreateShopReviewRequest request,
  ) async {
    final response = await _client.post(
      'api/shops/$shopId/reviews',
      request.toJson(),
    );
    return ShopReviewDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<ShopReviewHelpfulVoteResultDto> voteHelpful(
    String shopId,
    String reviewId, {
    required bool isHelpful,
  }) async {
    final response = await _client.post(
      'api/shops/$shopId/reviews/$reviewId/helpful',
      {'isHelpful': isHelpful},
    );
    return ShopReviewHelpfulVoteResultDto.fromJson(
      response.data as Map<String, dynamic>,
    );
  }
}
