import '../models/categories.dart';
import '../network/api_client.dart';

class CategoriesApi {
  CategoriesApi(this._client);

  final ApiClient _client;

  Future<List<CategoryDto>> getAll() async {
    final response = await _client.get('api/categories');
    final data = response.data as List<dynamic>? ?? [];
    return data.map((e) => CategoryDto.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<CategoryDto> getById(String id) async {
    final response = await _client.get('api/categories/$id');
    return CategoryDto.fromJson(response.data as Map<String, dynamic>);
  }
}
