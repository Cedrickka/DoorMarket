import '../models/me.dart';
import '../network/api_client.dart';

class AddressApi {
  AddressApi(this._client);

  final ApiClient _client;

  Future<List<AddressDto>> getAddresses() async {
    return _client.read(_client.get('api/me/addresses'), (data) {
      final rows = data as List<dynamic>? ?? [];
      return rows
          .map((e) => AddressDto.fromJson(e as Map<String, dynamic>))
          .toList();
    });
  }

  Future<AddressDto> create(CreateAddressRequest request) async {
    return _client.read(
      _client.post('api/me/addresses', request.toJson()),
      (data) => AddressDto.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<void> update(String id, UpdateAddressRequest request) async {
    await _client.read(
      _client.put('api/me/addresses/$id', request.toJson()),
      (_) => true,
    );
  }

  Future<void> delete(String id) async {
    await _client.read(_client.delete('api/me/addresses/$id'), (_) => true);
  }
}
