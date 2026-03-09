using DoorMarket.Application.DTOs.Auth;

namespace DoorMarket.Mobile.Services;

public sealed class AuthApiClient : ApiClientBase
{
    public AuthApiClient(HttpClient http) : base(http)
    {
    }

    public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct)
        => PostAsync<LoginResult>("api/auth/login", request, ct);

    public Task<LoginResult> LoginOtpAsync(LoginOtpRequest request, CancellationToken ct)
        => PostAsync<LoginResult>("api/auth/login-otp", request, ct);

    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
        => PostAsync<AuthResponse>("api/auth/register", request, ct);

    public Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
        => PostAsync("api/auth/verify-email", request, ct);

    public Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct)
        => PostAsync("api/auth/resend-verification", request, ct);

    public Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
        => PostAsync<AuthResponse>("api/auth/refresh", request, ct);

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        await PostAsync("api/auth/logout", request, ct);
    }
}
