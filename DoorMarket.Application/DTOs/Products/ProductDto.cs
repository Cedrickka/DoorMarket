namespace DoorMarket.Application.DTOs.Products;

public record ProductDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    Guid CategoryId,
    string CategoryName,
    string? CategoryNameEn,
    string Name,
    string? Description,
    decimal Price,
    decimal EffectivePrice,
    bool HasActivePromotion,
    bool IsPromotionEnabled,
    decimal? PromotionPrice,
    decimal? PromotionPercent,
    DateTime? PromotionStartUtc,
    DateTime? PromotionEndUtc,
    string Currency,
    int StockQty,
    bool IsActive,
    string? MainImageUrl,
    DateTime CreatedAtUtc,
    double? ShopRating,
    int ShopReviewCount
);
