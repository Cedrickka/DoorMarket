using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DoorMarket.Application.Interfaces.Auth;

namespace DoorMarket.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public bool IsAuthenticated => _http.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user is null) return null;

            var raw =
                user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _http.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public string? Role => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

}
