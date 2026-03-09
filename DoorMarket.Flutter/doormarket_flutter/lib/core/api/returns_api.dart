import '../models/common.dart';
import '../models/returns.dart';
import '../network/api_client.dart';
import '../network/query_params.dart';

class ReturnsApi {
  ReturnsApi(this._client);

  final ApiClient _client;

  Future<ReturnRequestDto> create(CreateReturnRequest request) async {
    final response = await _client.post('api/returns', request.toJson());
    return ReturnRequestDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<PagedResult<ReturnRequestDto>> getMine({
    String? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    final path = QueryParams.build('api/returns/mine', {
      'status': status,
      'page': page.toString(),
      'pageSize': pageSize.toString(),
    });
    final response = await _client.get(path);
    return PagedResult.fromJson(
      response.data as Map<String, dynamic>,
      (item) => ReturnRequestDto.fromJson(item as Map<String, dynamic>),
    );
  }

  Future<ReturnRequestDto> getById(String id) async {
    final response = await _client.get('api/returns/$id');
    return ReturnRequestDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<List<ReturnReasonDto>> getReasons() async {
    final response = await _client.get('api/returns/reasons');
    final data = response.data as List<dynamic>? ?? const [];
    return data
        .whereType<Map<String, dynamic>>()
        .map(ReturnReasonDto.fromJson)
        .toList(growable: false);
  }

  Future<List<ReturnTimelineEventDto>> getTimeline(String id) async {
    final response = await _client.get('api/returns/$id/timeline');
    final data = response.data as List<dynamic>? ?? const [];
    return data
        .whereType<Map<String, dynamic>>()
        .map(ReturnTimelineEventDto.fromJson)
        .toList(growable: false);
  }
}
