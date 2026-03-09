namespace DoorMarket.Application.DTOs.Products;

public record ProductCreateRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    bool IsPromotionEnabled,
    decimal? PromotionPrice,
    DateTime? PromotionStartUtc,
    DateTime? PromotionEndUtc,
    string Currency,
    int StockQty,
    string? MainImageUrl,
    decimal? PromotionPercent = null
);
