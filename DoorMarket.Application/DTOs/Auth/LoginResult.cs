namespace DoorMarket.Application.DTOs.Auth;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    bool OtpRequired
);

public record LoginOtpRequest(
    string Login,
    string Code
);
