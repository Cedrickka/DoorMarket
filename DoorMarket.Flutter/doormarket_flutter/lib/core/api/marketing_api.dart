import '../models/marketing.dart';
import '../network/api_client.dart';

class MarketingApi {
  MarketingApi(this._client);

  final ApiClient _client;

  Future<List<MarketingBannerDto>> getBanners({
    String? language,
    String? city,
    String? zone,
    String? categoryId,
    String? shopId,
  }) async {
    final query = <String, dynamic>{};
    if (language != null && language.isNotEmpty) query['lang'] = language;
    if (city != null && city.isNotEmpty) query['city'] = city;
    if (zone != null && zone.isNotEmpty) query['zone'] = zone;
    if (categoryId != null && categoryId.isNotEmpty) query['categoryId'] = categoryId;
    if (shopId != null && shopId.isNotEmpty) query['shopId'] = shopId;

    return _client.read(
      _client.get('api/marketing/banners', queryParameters: query),
      (data) {
        final items = data is List ? data : <dynamic>[];
        return items.map((e) => MarketingBannerDto.fromJson(e as Map<String, dynamic>)).toList();
      },
    );
  }

  Future<void> trackImpression(String bannerId) async {
    await _client.post('api/marketing/banners/$bannerId/impression', null);
  }

  Future<void> trackClick(String bannerId) async {
    await _client.post('api/marketing/banners/$bannerId/click', null);
  }
}
