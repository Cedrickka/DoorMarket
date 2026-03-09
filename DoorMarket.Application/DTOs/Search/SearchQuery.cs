namespace DoorMarket.Application.DTOs.Search;

public sealed record SearchQuery(
    string? Q = null,
    int Page = 1,
    int PageSize = 20,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = false,
    bool PromotedOnly = false,
    decimal? RatingMin = null,
    Guid? ShopId = null,
    Guid? CategoryId = null,
    string? City = null,
    string? CountryTag = null,
    string? Sort = null
);
