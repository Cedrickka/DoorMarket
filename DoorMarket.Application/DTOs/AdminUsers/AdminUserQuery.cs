namespace DoorMarket.Application.DTOs.AdminUsers;

public sealed record AdminUserQuery(
    string? Q = null,
    string? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);
