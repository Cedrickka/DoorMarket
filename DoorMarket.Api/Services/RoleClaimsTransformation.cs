using System.Security.Claims;
using DoorMarket.Domain.Enums;
using Microsoft.AspNetCore.Authentication;

namespace DoorMarket.Api.Services;

public sealed class RoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var roleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        if (roleClaims.Count == 0)
        {
            roleClaims = identity.FindAll("role").ToList();
        }

        foreach (var claim in roleClaims)
        {
            var value = claim.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (int.TryParse(value, out var roleId) && Enum.IsDefined(typeof(UserRole), roleId))
            {
                var roleName = ((UserRole)roleId).ToString();
                EnsureRole(identity, roleName);

                if (roleName == nameof(UserRole.SuperAdmin))
                {
                    EnsureRole(identity, nameof(UserRole.Admin));
                }

                continue;
            }

            if (string.Equals(value, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
            {
                EnsureRole(identity, nameof(UserRole.Admin));
            }
        }

        return Task.FromResult(principal);
    }

    private static void EnsureRole(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
}
