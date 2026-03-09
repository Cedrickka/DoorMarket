using DoorMarket.Domain.Entities;

namespace DoorMarket.Application.Interfaces.Auth;

public interface IJwtTokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(User user);
    string CreateRefreshToken();
}
