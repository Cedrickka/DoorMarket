using DoorMarket.Application.DTOs.Auth;
using DoorMarket.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
    {
        var res = await _auth.RegisterAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("login-otp")]
    public async Task<ActionResult<LoginResult>> LoginOtp(LoginOtpRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginOtpAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var res = await _auth.RefreshAsync(req, ct);
        return Ok(res);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest req, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(req, ct);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest req, CancellationToken ct)
    {
        await _auth.ResendVerificationAsync(req, ct);
        return NoContent();
    }

    [HttpPost("resend-login-otp")]
    public async Task<IActionResult> ResendLoginOtp(ResendLoginOtpRequest req, CancellationToken ct)
    {
        await _auth.ResendLoginOtpAsync(req, ct);
        return NoContent();
    }
}

