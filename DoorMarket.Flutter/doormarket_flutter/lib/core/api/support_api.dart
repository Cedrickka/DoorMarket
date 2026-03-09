import '../network/api_client.dart';

class SupportApi {
  SupportApi(this._client);

  final ApiClient _client;

  Future<void> sendContact({
    required String name,
    required String email,
    required String subject,
    required String message,
  }) async {
    await _client.post('api/support/contact', {
      'name': name,
      'email': email,
      'subject': subject,
      'message': message,
    });
  }
}
