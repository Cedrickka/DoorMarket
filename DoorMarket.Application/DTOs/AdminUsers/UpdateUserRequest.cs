namespace DoorMarket.Application.DTOs.AdminUsers;

public record UpdateUserRequest(
    string? Phone,
    string Role,
    bool IsActive
);
