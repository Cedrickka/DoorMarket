namespace DoorMarket.Application.DTOs.AdminUsers;

public record CreateUserRequest(
    string Email,
    string Password,
    string? Phone,
    string Role
);
