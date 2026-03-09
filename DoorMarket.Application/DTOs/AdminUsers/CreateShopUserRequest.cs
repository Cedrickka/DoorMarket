namespace DoorMarket.Application.DTOs.AdminUsers;

public record CreateShopUserRequest(
    string Email,
    string Password,
    string? Phone
);
