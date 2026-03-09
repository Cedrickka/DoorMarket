using DoorMarket.Application.DTOs.Auth;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace DoorMarket.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly DoorMarketDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IAuthEmailSender _emailSender;
    private readonly JwtOptions _opt;
    private readonly PasswordHasher<User> _hasher = new();
    private const string PurposeRegister = "register";
    private const string PurposeLogin = "login";

    public AuthService(
        DoorMarketDbContext db,
        IJwtTokenService jwt,
        IAuthEmailSender emailSender,
        IOptions<JwtOptions> opt)
    {
        _db = db;
        _jwt = jwt;
        _emailSender = emailSender;
        _opt = opt.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        var existsEmail = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (existsEmail) throw new InvalidOperationException("Email déjà utilisé.");

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var existsPhone = await _db.Users.AnyAsync(x => x.Phone == phone, ct);
            if (existsPhone) throw new InvalidOperationException("Telephone deja utilise. Laissez ce champ vide ou utilisez un autre numero.");
        }

        var (verificationCode, expiresAtUtc) = CreateVerificationCode();

        var user = new User
        {
            Email = email,
            Phone = phone,
            Role = UserRole.Client,
            IsActive = true,
            EmailConfirmed = false,
            EmailVerificationCode = verificationCode,
            EmailVerificationExpiresAtUtc = expiresAtUtc,
            EmailVerificationPurpose = PurposeRegister
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);

        await _db.SaveChangesAsync(ct);
        await _emailSender.SendVerificationCodeAsync(email, verificationCode, ct);

        return new AuthResponse(string.Empty, string.Empty, DateTime.UtcNow);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();

        var user = await _db.Users
            .Include(x => x.RefreshTokens)
            .FirstOrDefaultAsync(x => x.Email == login.ToLowerInvariant() || x.Phone == login, ct);

        if (user is null || !user.IsActive)
            throw new InvalidOperationException("Identifiants invalides.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Identifiants invalides.");

        if (RequiresLoginOtp(user))
        {
            var (verificationCode, expiresAtUtc) = CreateVerificationCode();
            user.EmailVerificationCode = verificationCode;
            user.EmailVerificationExpiresAtUtc = expiresAtUtc;
            user.EmailVerificationPurpose = PurposeLogin;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await _emailSender.SendVerificationCodeAsync(user.Email, verificationCode, ct);

            return new LoginResult(string.Empty, string.Empty, DateTime.UtcNow, true);
        }

        // Rotation refresh token : révoquer anciens tokens non expirés (simple V1)
        foreach (var rt in user.RefreshTokens.Where(x => !x.IsRevoked && !x.IsExpired))
            rt.RevokedAtUtc = DateTime.UtcNow;

        var refresh = CreateAndAttachRefreshToken(user);
        await _db.SaveChangesAsync(ct);

        var (accessToken, exp) = _jwt.CreateAccessToken(user);
        return new LoginResult(accessToken, refresh.Token, exp, false);
    }

    public async Task<LoginResult> LoginOtpAsync(LoginOtpRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();
        var code = (request.Code ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Code de verification requis.");
        }

        var user = await _db.Users
            .Include(x => x.RefreshTokens)
            .FirstOrDefaultAsync(x => x.Email == login.ToLowerInvariant() || x.Phone == login, ct);

        if (user is null || !user.IsActive)
            throw new InvalidOperationException("Identifiants invalides.");
        if (!RequiresLoginOtp(user))
            throw new InvalidOperationException("Aucune verification requise pour ce compte.");

        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode) ||
            user.EmailVerificationExpiresAtUtc is null ||
            user.EmailVerificationExpiresAtUtc < DateTime.UtcNow ||
            !string.Equals(user.EmailVerificationPurpose, PurposeLogin, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(user.EmailVerificationCode, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Code de verification invalide ou expire.");
        }

        user.EmailVerificationCode = null;
        user.EmailVerificationExpiresAtUtc = null;
        user.EmailVerificationPurpose = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Rotation refresh token : révoquer anciens tokens non expirés (simple V1)
        foreach (var rt in user.RefreshTokens.Where(x => !x.IsRevoked && !x.IsExpired))
            rt.RevokedAtUtc = DateTime.UtcNow;

        var refresh = CreateAndAttachRefreshToken(user);
        await _db.SaveChangesAsync(ct);

        var (accessToken, exp) = _jwt.CreateAccessToken(user);
        return new LoginResult(accessToken, refresh.Token, exp, false);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = request.RefreshToken.Trim();

        var rt = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == token, ct);

        if (rt is null || rt.IsRevoked || rt.IsExpired || !rt.User.IsActive)
            throw new InvalidOperationException("Refresh token invalide.");

        // Rotation : révoquer l’ancien et en créer un nouveau
        rt.RevokedAtUtc = DateTime.UtcNow;

        var newRt = CreateAndAttachRefreshToken(rt.User);
        await _db.SaveChangesAsync(ct);

        var (accessToken, exp) = _jwt.CreateAccessToken(rt.User);
        return new AuthResponse(accessToken, newRt.Token, exp);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var token = refreshToken.Trim();
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token, ct);
        if (rt is null) return;

        rt.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var code = (request.Code ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Code de verification requis.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new InvalidOperationException("Compte introuvable.");

        if (user.EmailConfirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode) ||
            user.EmailVerificationExpiresAtUtc is null ||
            user.EmailVerificationExpiresAtUtc < DateTime.UtcNow ||
            !string.Equals(user.EmailVerificationPurpose, PurposeRegister, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(user.EmailVerificationCode, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Code de verification invalide ou expire.");
        }

        user.EmailConfirmed = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationExpiresAtUtc = null;
        user.EmailVerificationPurpose = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new InvalidOperationException("Compte introuvable.");

        if (user.EmailConfirmed)
        {
            return;
        }

        var (verificationCode, expiresAtUtc) = CreateVerificationCode();
        user.EmailVerificationCode = verificationCode;
        user.EmailVerificationExpiresAtUtc = expiresAtUtc;
        user.EmailVerificationPurpose = PurposeRegister;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _emailSender.SendVerificationCodeAsync(email, verificationCode, ct);
    }

    public async Task ResendLoginOtpAsync(ResendLoginOtpRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == login.ToLowerInvariant() || x.Phone == login, ct)
            ?? throw new InvalidOperationException("Identifiants invalides.");

        if (!user.IsActive)
            throw new InvalidOperationException("Identifiants invalides.");

        if (!RequiresLoginOtp(user))
            throw new InvalidOperationException("Aucune verification requise pour ce compte.");

        var (verificationCode, expiresAtUtc) = CreateVerificationCode();
        user.EmailVerificationCode = verificationCode;
        user.EmailVerificationExpiresAtUtc = expiresAtUtc;
        user.EmailVerificationPurpose = PurposeLogin;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _emailSender.SendVerificationCodeAsync(user.Email, verificationCode, ct);
    }

    private RefreshToken CreateAndAttachRefreshToken(User user)
    {
        var rt = new RefreshToken
        {
            UserId = user.Id,
            Token = _jwt.CreateRefreshToken(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(rt);
        return rt;
    }

    private static string NormalizeEmail(string value)
    {
        var email = value.Trim().ToLowerInvariant();
        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            throw new InvalidOperationException("Email invalide.");
        }

        return email;
    }

    private static (string code, DateTime expiresAtUtc) CreateVerificationCode()
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        return (code, DateTime.UtcNow.AddMinutes(15));
    }

    private static bool RequiresLoginOtp(User user)
        => false;
}
