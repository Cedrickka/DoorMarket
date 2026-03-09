using DoorMarket.Application.DTOs.Me;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Storage;
using DoorMarket.Api.Security;
using DoorMarket.Api.Utils;
using DoorMarket.Domain.Enums;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuthEmailSender _emailSender;
    private readonly IFileStorage _storage;
    private readonly PasswordHasher<User> _hasher = new();
    private const string PurposePhoneChange = "phone_change";
    private const string PurposePasswordChange = "password_change";

    public MeController(DoorMarketDbContext db, ICurrentUserService current, IAuthEmailSender emailSender, IFileStorage storage)
    {
        _db = db;
        _current = current;
        _emailSender = emailSender;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<MeDto>> Get(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        return Ok(new MeDto(u.Id, u.Email, u.Phone, u.ProfileImageUrl, u.Role.ToString(), u.IsActive));
    }

    [HttpPost("upload-photo")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProfileImageUploadResponse>> UploadPhoto(IFormFile file, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var validated = await UploadSecurityValidator.ValidateImageAsync(file, ct);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var folder = $"profiles/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        await using var stream = file.OpenReadStream();
        var (_, url, _) = await _storage.SaveAsync(stream, validated.FileName, validated.ContentType, folder, ct);
        var publicUrl = ResponseUrlNormalizer.ToAbsoluteUrl(url, Request);

        user.ProfileImageUrl = publicUrl ?? url;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new ProfileImageUploadResponse(user.ProfileImageUrl));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateMeRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (u.Role == UserRole.Client)
        {
            throw new InvalidOperationException("Veuillez confirmer le changement par OTP.");
        }

        // V1: on autorise update du phone seulement (email non modifiable)
        u.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("request-phone-change")]
    public async Task<IActionResult> RequestPhoneChange(RequestPhoneChangeRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (u.Role != UserRole.Client)
        {
            throw new InvalidOperationException("Operation reservee aux clients.");
        }

        var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var existsPhone = await _db.Users.AnyAsync(x => x.Phone == phone && x.Id != u.Id, ct);
            if (existsPhone)
                throw new InvalidOperationException("Telephone deja utilise. Laissez ce champ vide ou utilisez un autre numero.");
        }

        var (code, expiresAtUtc) = CreateVerificationCode();
        u.PendingPhone = phone;
        u.EmailVerificationCode = code;
        u.EmailVerificationExpiresAtUtc = expiresAtUtc;
        u.EmailVerificationPurpose = PurposePhoneChange;
        u.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _emailSender.SendVerificationCodeAsync(u.Email, code, ct);

        return NoContent();
    }

    [HttpPost("confirm-phone-change")]
    public async Task<IActionResult> ConfirmPhoneChange(ConfirmPhoneChangeRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (u.Role != UserRole.Client)
        {
            throw new InvalidOperationException("Operation reservee aux clients.");
        }

        var code = (req.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Code de verification requis.");

        if (string.IsNullOrWhiteSpace(u.EmailVerificationCode) ||
            u.EmailVerificationExpiresAtUtc is null ||
            u.EmailVerificationExpiresAtUtc < DateTime.UtcNow ||
            !string.Equals(u.EmailVerificationPurpose, PurposePhoneChange, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(u.EmailVerificationCode, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Code de verification invalide ou expire.");
        }

        u.Phone = u.PendingPhone;
        u.PendingPhone = null;
        u.EmailVerificationCode = null;
        u.EmailVerificationExpiresAtUtc = null;
        u.EmailVerificationPurpose = null;
        u.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("request-password-change")]
    public async Task<IActionResult> RequestPasswordChange(RequestPasswordChangeRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (u.Role != UserRole.Client)
        {
            throw new InvalidOperationException("Operation reservee aux clients.");
        }

        var password = req.Password ?? string.Empty;
        if (password.Length < 6)
            throw new InvalidOperationException("Mot de passe trop court.");

        var (code, expiresAtUtc) = CreateVerificationCode();
        u.PendingPasswordHash = _hasher.HashPassword(u, password);
        u.EmailVerificationCode = code;
        u.EmailVerificationExpiresAtUtc = expiresAtUtc;
        u.EmailVerificationPurpose = PurposePasswordChange;
        u.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _emailSender.SendVerificationCodeAsync(u.Email, code, ct);

        return NoContent();
    }

    [HttpPost("confirm-password-change")]
    public async Task<IActionResult> ConfirmPasswordChange(ConfirmPasswordChangeRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var u = await _db.Users
            .Include(x => x.RefreshTokens)
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (u.Role != UserRole.Client)
        {
            throw new InvalidOperationException("Operation reservee aux clients.");
        }

        var code = (req.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Code de verification requis.");

        if (string.IsNullOrWhiteSpace(u.EmailVerificationCode) ||
            u.EmailVerificationExpiresAtUtc is null ||
            u.EmailVerificationExpiresAtUtc < DateTime.UtcNow ||
            !string.Equals(u.EmailVerificationPurpose, PurposePasswordChange, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(u.EmailVerificationCode, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Code de verification invalide ou expire.");
        }

        if (string.IsNullOrWhiteSpace(u.PendingPasswordHash))
            throw new InvalidOperationException("Aucun mot de passe en attente.");

        u.PasswordHash = u.PendingPasswordHash;
        u.PendingPasswordHash = null;
        u.EmailVerificationCode = null;
        u.EmailVerificationExpiresAtUtc = null;
        u.EmailVerificationPurpose = null;
        u.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var rt in u.RefreshTokens.Where(x => !x.IsRevoked && !x.IsExpired))
            rt.RevokedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static (string code, DateTime expiresAtUtc) CreateVerificationCode()
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        return (code, DateTime.UtcNow.AddMinutes(15));
    }

    // ---------------- Push devices ----------------

    [HttpGet("push-devices")]
    public async Task<ActionResult<List<PushDeviceDto>>> GetPushDevices(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var rows = await _db.UserPushDevices.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Select(x => new PushDeviceDto(
                x.Id,
                x.Platform,
                x.DeviceId,
                x.DeviceModel,
                x.AppVersion,
                x.IsActive,
                x.LastSeenAtUtc))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("push-devices/register")]
    public async Task<ActionResult<PushDeviceDto>> RegisterPushDevice([FromBody] RegisterPushDeviceRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var token = (req.Token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Push token requis.");
        }

        var platform = NormalizePushPlatform(req.Platform);
        var now = DateTime.UtcNow;

        var row = await _db.UserPushDevices.FirstOrDefaultAsync(x => x.Token == token, ct);
        if (row is null)
        {
            row = new UserPushDevice
            {
                UserId = userId,
                Platform = platform,
                Token = token,
                DeviceId = NormalizeShort(req.DeviceId, 128),
                DeviceModel = NormalizeShort(req.DeviceModel, 128),
                AppVersion = NormalizeShort(req.AppVersion, 64),
                IsActive = true,
                LastSeenAtUtc = now
            };
            _db.UserPushDevices.Add(row);
        }
        else
        {
            row.UserId = userId;
            row.Platform = platform;
            row.DeviceId = NormalizeShort(req.DeviceId, 128);
            row.DeviceModel = NormalizeShort(req.DeviceModel, 128);
            row.AppVersion = NormalizeShort(req.AppVersion, 64);
            row.IsActive = true;
            row.LastSeenAtUtc = now;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new PushDeviceDto(
            row.Id,
            row.Platform,
            row.DeviceId,
            row.DeviceModel,
            row.AppVersion,
            row.IsActive,
            row.LastSeenAtUtc));
    }

    [HttpDelete("push-devices/unregister")]
    public async Task<IActionResult> UnregisterPushDevice([FromQuery] string token, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var normalizedToken = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return NoContent();
        }

        var row = await _db.UserPushDevices.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Token == normalizedToken,
            ct);
        if (row is null)
        {
            return NoContent();
        }

        row.IsActive = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------- Addresses ----------------

    [HttpGet("addresses")]
    public async Task<ActionResult<List<AddressDto>>> GetAddresses(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var items = await _db.UserAddresses.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreatedAtUtc)
            .GroupJoin(
                _db.DeliveryZones.AsNoTracking(),
                a => a.DeliveryZoneId,
                z => z.Id,
                (a, zs) => new { Address = a, Zone = zs.FirstOrDefault() })
            .Select(x => new AddressDto(
                x.Address.Id, x.Address.Label, x.Address.FullName, x.Address.Phone,
                x.Address.Country, x.Address.City, x.Address.District, x.Address.DeliveryZoneId, x.Zone != null ? x.Zone.Name : null, x.Address.Street, x.Address.Landmark, x.Address.IsDefault
            ))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("addresses")]
    public async Task<ActionResult<AddressDto>> CreateAddress(CreateAddressRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var hasZones = await _db.DeliveryZones.AsNoTracking().AnyAsync(z => z.IsActive, ct);
        if (hasZones && !req.DeliveryZoneId.HasValue)
        {
            throw new InvalidOperationException("Zone de livraison obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(req.FullName)) throw new InvalidOperationException("Nom obligatoire.");
        if (string.IsNullOrWhiteSpace(req.Phone)) throw new InvalidOperationException("Téléphone obligatoire.");
        if (string.IsNullOrWhiteSpace(req.City)) throw new InvalidOperationException("Ville obligatoire.");
        if (string.IsNullOrWhiteSpace(req.District)) throw new InvalidOperationException("Commune/quartier obligatoire.");
        if (string.IsNullOrWhiteSpace(req.Street)) throw new InvalidOperationException("Adresse obligatoire.");
        if (req.DeliveryZoneId.HasValue)
        {
            var zoneExists = await _db.DeliveryZones.AnyAsync(z => z.Id == req.DeliveryZoneId.Value && z.IsActive, ct);
            if (!zoneExists) throw new InvalidOperationException("Zone de livraison invalide.");
        }

        if (req.IsDefault)
        {
            var others = await _db.UserAddresses.Where(x => x.UserId == userId && x.IsDefault).ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
        }

        var a = new Domain.Entities.UserAddress
        {
            UserId = userId,
            Label = string.IsNullOrWhiteSpace(req.Label) ? "Maison" : req.Label.Trim(),
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            Country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country.Trim(),
            City = req.City.Trim(),
            District = req.District.Trim(),
            DeliveryZoneId = req.DeliveryZoneId,
            Street = req.Street.Trim(),
            Landmark = string.IsNullOrWhiteSpace(req.Landmark) ? null : req.Landmark.Trim(),
            IsDefault = req.IsDefault
        };

        _db.UserAddresses.Add(a);
        await _db.SaveChangesAsync(ct);

        var zoneName = a.DeliveryZoneId.HasValue
            ? await _db.DeliveryZones.AsNoTracking().Where(z => z.Id == a.DeliveryZoneId.Value).Select(z => z.Name).FirstOrDefaultAsync(ct)
            : null;

        return Ok(new AddressDto(
            a.Id, a.Label, a.FullName, a.Phone,
            a.Country, a.City, a.District, a.DeliveryZoneId, zoneName, a.Street, a.Landmark, a.IsDefault
        ));
    }

    [HttpPut("addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, UpdateAddressRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var a = await _db.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Adresse introuvable.");

        var hasZones = await _db.DeliveryZones.AsNoTracking().AnyAsync(z => z.IsActive, ct);
        if (hasZones && !req.DeliveryZoneId.HasValue)
        {
            throw new InvalidOperationException("Zone de livraison obligatoire.");
        }

        if (req.IsDefault)
        {
            var others = await _db.UserAddresses.Where(x => x.UserId == userId && x.IsDefault && x.Id != id).ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
        }

        if (req.DeliveryZoneId.HasValue)
        {
            var zoneExists = await _db.DeliveryZones.AnyAsync(z => z.Id == req.DeliveryZoneId.Value && z.IsActive, ct);
            if (!zoneExists) throw new InvalidOperationException("Zone de livraison invalide.");
        }

        a.Label = string.IsNullOrWhiteSpace(req.Label) ? "Maison" : req.Label.Trim();
        a.FullName = req.FullName.Trim();
        a.Phone = req.Phone.Trim();
        a.Country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country.Trim();
        a.City = req.City.Trim();
        a.District = req.District.Trim();
        a.DeliveryZoneId = req.DeliveryZoneId;
        a.Street = req.Street.Trim();
        a.Landmark = string.IsNullOrWhiteSpace(req.Landmark) ? null : req.Landmark.Trim();
        a.IsDefault = req.IsDefault;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var a = await _db.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Adresse introuvable.");

        _db.UserAddresses.Remove(a);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string NormalizePushPlatform(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "android" => "fcm",
            "fcm" => "fcm",
            "ios" => "apns",
            "apns" => "apns",
            _ => "fcm"
        };
    }

    private static string? NormalizeShort(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public sealed record RegisterPushDeviceRequest(
        string? Platform,
        string? Token,
        string? DeviceId,
        string? DeviceModel,
        string? AppVersion);

    public sealed record PushDeviceDto(
        Guid Id,
        string Platform,
        string? DeviceId,
        string? DeviceModel,
        string? AppVersion,
        bool IsActive,
        DateTime LastSeenAtUtc);
}

