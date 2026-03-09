namespace DoorMarket.Application.DTOs.Products;

public record ProductUpdateRequest(
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
    bool IsActive,
    string? MainImageUrl,
    decimal? PromotionPercent = null
);
