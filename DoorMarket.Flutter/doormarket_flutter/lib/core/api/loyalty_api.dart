import '../models/loyalty.dart';
import '../network/api_client.dart';

class LoyaltyApi {
  LoyaltyApi(this._client);

  final ApiClient _client;

  Future<LoyaltyWalletDto> getMyWallet() async {
    final response = await _client.get('api/loyalty/me');
    return LoyaltyWalletDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<List<LoyaltyLedgerEntryDto>> getMyHistory({
    int page = 1,
    int pageSize = 20,
  }) async {
    final response = await _client
        .get('api/loyalty/me/history?page=$page&pageSize=$pageSize');
    final data = response.data as List<dynamic>? ?? const [];
    return data
        .whereType<Map<String, dynamic>>()
        .map(LoyaltyLedgerEntryDto.fromJson)
        .toList(growable: false);
  }
}

