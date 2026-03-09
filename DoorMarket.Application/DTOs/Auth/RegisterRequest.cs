namespace DoorMarket.Application.DTOs.Auth;

public record RegisterRequest(
    string Email,
    string? Phone,
    string Password
);

public record VerifyEmailRequest(
    string Email,
    string Code
);

public record ResendVerificationRequest(
    string Email
);
