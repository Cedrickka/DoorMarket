namespace DoorMarket.Application.DTOs.Shops;

public record ShopCreateRequest(
    string Name,
    string CountryTag,
    string City,
    string? ImageUrl = null
);
