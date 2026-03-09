namespace DoorMarket.Application.DTOs.Shops;

public record ShopQuery(
    string? CountryTag = null,
    string? City = null,
    string? Q = null,
    int Page = 1,
    int PageSize = 20,
    bool VerifiedOnly = false,
    Guid? CategoryId = null,
    bool? Recommended = null
);
