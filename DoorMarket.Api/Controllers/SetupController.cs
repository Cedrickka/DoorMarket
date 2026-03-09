using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly PasswordHasher<User> _hasher = new();

    public SetupController(DoorMarketDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUser([FromBody] SetupCreateUserRequest req, CancellationToken ct)
    {
        var enabled = _cfg.GetValue<bool>("AdminSetup:Enabled");
        if (!enabled) return Forbid("Setup disabled.");

        var key = _cfg["AdminSetup:Key"];
        var headerKey = Request.Headers["X-Setup-Key"].ToString();

        if (string.IsNullOrWhiteSpace(key) || headerKey != key)
            return Unauthorized("Invalid setup key.");

        var email = req.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) return Conflict("User already exists.");

        var user = new User
        {
            Email = email,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Role = req.Role,
            IsActive = true
        };

        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Email, Role = user.Role.ToString() });
    }
}

public record SetupCreateUserRequest(
    string Email,
    string Password,
    UserRole Role,
    string? Phone
);
