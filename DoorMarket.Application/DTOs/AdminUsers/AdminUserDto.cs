namespace DoorMarket.Application.DTOs.AdminUsers;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string? Phone,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    Guid? ShopId,
    string? ShopName
);
