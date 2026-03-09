import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';

import '../models/me.dart';
import '../network/api_client.dart';

class MeApi {
  MeApi(this._client);

  final ApiClient _client;

  Future<MeDto> getMe() async {
    final response = await _client.get('api/me');
    return MeDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<MeDto> getMeNoRefresh() async {
    final response = await _client.raw.get(
      'api/me',
      options: Options(extra: {'dm_skip_refresh': true}),
    );
    return MeDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> updateMe(UpdateMeRequest request) async {
    await _client.put('api/me', request.toJson());
  }

  Future<void> requestPhoneChange(RequestPhoneChangeRequest request) async {
    await _client.post('api/me/request-phone-change', request.toJson());
  }

  Future<void> confirmPhoneChange(ConfirmPhoneChangeRequest request) async {
    await _client.post('api/me/confirm-phone-change', request.toJson());
  }

  Future<void> requestPasswordChange(
      RequestPasswordChangeRequest request) async {
    await _client.post('api/me/request-password-change', request.toJson());
  }

  Future<void> confirmPasswordChange(
      ConfirmPasswordChangeRequest request) async {
    await _client.post('api/me/confirm-password-change', request.toJson());
  }

  Future<ProfileImageUploadResponse> uploadProfilePhoto({
    required List<int> bytes,
    required String fileName,
    required String contentType,
  }) async {
    final form = FormData.fromMap({
      'file': MultipartFile.fromBytes(bytes,
          filename: fileName, contentType: MediaType.parse(contentType)),
    });
    final response = await _client.post('api/me/upload-photo', form);
    return ProfileImageUploadResponse.fromJson(
        response.data as Map<String, dynamic>);
  }

  Future<List<PushDeviceDto>> getPushDevices() async {
    final response = await _client.get('api/me/push-devices');
    final data = response.data as List<dynamic>? ?? const [];
    return data
        .whereType<Map<String, dynamic>>()
        .map(PushDeviceDto.fromJson)
        .toList(growable: false);
  }

  Future<PushDeviceDto> registerPushDevice(
      RegisterPushDeviceRequest request) async {
    final response =
        await _client.post('api/me/push-devices/register', request.toJson());
    return PushDeviceDto.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> unregisterPushDevice(String token) async {
    final normalized = token.trim();
    if (normalized.isEmpty) {
      return;
    }

    await _client.delete(
      'api/me/push-devices/unregister',
      queryParameters: {'token': normalized},
    );
  }
}
