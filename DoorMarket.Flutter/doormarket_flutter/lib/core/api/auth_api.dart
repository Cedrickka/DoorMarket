import '../models/auth.dart';
import '../network/api_client.dart';

class AuthApi {
  AuthApi(this._client);

  final ApiClient _client;

  Future<LoginResult> login(LoginRequest request) async {
    return _client.read<LoginResult>(
      _client.post('api/auth/login', request.toJson()),
      (data) => LoginResult.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<LoginResult> loginOtp(LoginOtpRequest request) async {
    return _client.read<LoginResult>(
      _client.post('api/auth/login-otp', request.toJson()),
      (data) => LoginResult.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<AuthResponse> register(RegisterRequest request) async {
    return _client.read<AuthResponse>(
      _client.post('api/auth/register', request.toJson()),
      (data) => AuthResponse.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<void> verifyEmail(VerifyEmailRequest request) async {
    await _client.post('api/auth/verify-email', request.toJson());
  }

  Future<void> resendVerification(ResendVerificationRequest request) async {
    await _client.post('api/auth/resend-verification', request.toJson());
  }

  Future<AuthResponse> refresh(RefreshRequest request) async {
    return _client.read<AuthResponse>(
      _client.post('api/auth/refresh', request.toJson()),
      (data) => AuthResponse.fromJson(data as Map<String, dynamic>),
    );
  }

  Future<void> logout(RefreshRequest request) async {
    await _client.post('api/auth/logout', request.toJson());
  }
}
