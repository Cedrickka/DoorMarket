class AuthResponse {
  final String accessToken;
  final String refreshToken;
  final DateTime expiresAtUtc;

  AuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAtUtc,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
    );
  }
}

class LoginResult {
  final String accessToken;
  final String refreshToken;
  final DateTime expiresAtUtc;
  final bool otpRequired;

  LoginResult({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAtUtc,
    required this.otpRequired,
  });

  factory LoginResult.fromJson(Map<String, dynamic> json) {
    return LoginResult(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      otpRequired: json['otpRequired'] as bool? ?? false,
    );
  }
}

class LoginRequest {
  final String login;
  final String password;

  LoginRequest({required this.login, required this.password});

  Map<String, dynamic> toJson() => {
        'login': login,
        'password': password,
      };
}

class LoginOtpRequest {
  final String login;
  final String code;

  LoginOtpRequest({required this.login, required this.code});

  Map<String, dynamic> toJson() => {
        'login': login,
        'code': code,
      };
}

class RegisterRequest {
  final String email;
  final String? phone;
  final String password;

  RegisterRequest({required this.email, this.phone, required this.password});

  Map<String, dynamic> toJson() => {
        'email': email,
        'phone': phone,
        'password': password,
      };
}

class VerifyEmailRequest {
  final String email;
  final String code;

  VerifyEmailRequest({required this.email, required this.code});

  Map<String, dynamic> toJson() => {
        'email': email,
        'code': code,
      };
}

class ResendVerificationRequest {
  final String email;

  ResendVerificationRequest({required this.email});

  Map<String, dynamic> toJson() => {'email': email};
}

class RefreshRequest {
  final String refreshToken;

  RefreshRequest({required this.refreshToken});

  Map<String, dynamic> toJson() => {'refreshToken': refreshToken};
}

class ResendLoginOtpRequest {
  final String login;

  ResendLoginOtpRequest({required this.login});

  Map<String, dynamic> toJson() => {'login': login};
}
