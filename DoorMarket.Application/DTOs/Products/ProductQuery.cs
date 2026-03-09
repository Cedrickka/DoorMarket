namespace DoorMarket.Application.DTOs.Products;

public record ProductQuery(
    Guid? ShopId = null,
    Guid? CategoryId = null,
    string? CountryTag = null,
    string? City = null,
    string? Q = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool InStockOnly = false,
    bool ActiveOnly = true,
    bool PromotedOnly = false,
    int Page = 1,
    int PageSize = 20
);
