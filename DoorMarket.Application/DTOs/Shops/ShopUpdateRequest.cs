namespace DoorMarket.Application.DTOs.Shops;

public record ShopUpdateRequest(
    string Name,
    string CountryTag,
    string City,
    string? ImageUrl = null
);
