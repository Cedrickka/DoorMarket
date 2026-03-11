import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/auth_api.dart';
import '../api/me_api.dart';
import '../models/auth.dart';
import '../models/me.dart';
import '../network/api_exception.dart';
import '../services/push_device_service.dart';
import 'auth_session.dart';
import 'auth_state.dart';

class AuthController extends StateNotifier<AuthState> {
  AuthController({
    required AuthSession session,
    required AuthApi authApi,
    required MeApi meApi,
    required PushDeviceService pushDeviceService,
  })  : _session = session,
        _authApi = authApi,
        _meApi = meApi,
        _pushDeviceService = pushDeviceService,
        super(AuthState.loading());

  final AuthSession _session;
  final AuthApi _authApi;
  final MeApi _meApi;
  final PushDeviceService _pushDeviceService;

  Future<void> initialize() async {
    state = AuthState.loading();
    try {
      final ok = await _session.initialize();
      if (!ok) {
        state = AuthState.unauthenticated();
        return;
      }

      final me = await _safeLoadMe();
      state = AuthState(isAuthenticated: true, isLoading: false, me: me);
      await _syncPushRegistration();
    } catch (_) {
      // Prevent startup crashes on unexpected bootstrap exceptions in release.
      state = AuthState.unauthenticated();
    }
  }

  Future<LoginResult> login(LoginRequest request) async {
    final result = await _authApi.login(request);
    if (!result.otpRequired) {
      await _session.signInWithLogin(result);
      final me = await _safeLoadMe();
      state = AuthState(isAuthenticated: true, isLoading: false, me: me);
      await _syncPushRegistration();
    }
    return result;
  }

  Future<AuthResponse> register(RegisterRequest request) async {
    final response = await _authApi.register(request);
    return response;
  }

  Future<void> verifyEmail(VerifyEmailRequest request) async {
    await _authApi.verifyEmail(request);
  }

  Future<void> resendVerification(ResendVerificationRequest request) async {
    await _authApi.resendVerification(request);
  }

  Future<void> logout() async {
    await _pushDeviceService.unregisterForLogout();
    await _session.signOut();
    state = AuthState.unauthenticated();
  }

  Future<MeDto?> refreshMe() async {
    final me = await _safeLoadMe();
    if (state.isAuthenticated) {
      state = state.copyWith(me: me);
    }
    return me;
  }

  Future<MeDto?> _safeLoadMe() async {
    try {
      return await _meApi.getMe();
    } on ApiException {
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<void> _syncPushRegistration() async {
    try {
      await _pushDeviceService.syncWithAuth(state.isAuthenticated);
    } catch (_) {}
  }
}
