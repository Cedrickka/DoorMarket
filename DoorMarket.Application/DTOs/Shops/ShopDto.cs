namespace DoorMarket.Application.DTOs.Shops;

public record ShopDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    string CountryTag,
    string City,
    bool IsVerified,
    DateTime CreatedAtUtc,
    double? Rating,
    int ReviewCount
);
