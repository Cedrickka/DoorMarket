using DoorMarket.Application.DTOs.Auth;

namespace DoorMarket.Application.Interfaces.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<LoginResult> LoginOtpAsync(LoginOtpRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(string refreshToken, CancellationToken ct);
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct);
    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct);
    Task ResendLoginOtpAsync(ResendLoginOtpRequest request, CancellationToken ct);
}
