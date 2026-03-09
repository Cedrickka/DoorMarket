import '../models/me.dart';

class AuthState {
  final bool isAuthenticated;
  final bool isLoading;
  final MeDto? me;
  final String? roleMismatchMessage;

  const AuthState({
    required this.isAuthenticated,
    required this.isLoading,
    this.me,
    this.roleMismatchMessage,
  });

  factory AuthState.unauthenticated() => const AuthState(isAuthenticated: false, isLoading: false);

  factory AuthState.loading() => const AuthState(isAuthenticated: false, isLoading: true);

  AuthState copyWith({
    bool? isAuthenticated,
    bool? isLoading,
    MeDto? me,
    String? roleMismatchMessage,
  }) {
    return AuthState(
      isAuthenticated: isAuthenticated ?? this.isAuthenticated,
      isLoading: isLoading ?? this.isLoading,
      me: me ?? this.me,
      roleMismatchMessage: roleMismatchMessage,
    );
  }
}
