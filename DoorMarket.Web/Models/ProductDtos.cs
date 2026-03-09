namespace DoorMarket.Web.Models;

public record ProductListItemDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    Guid CategoryId,
    string CategoryName,
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
    DateTime CreatedAtUtc
);

public record ProductDetailDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    Guid CategoryId,
    string CategoryName,
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
    DateTime CreatedAtUtc
);

public record ProductUpsertRequest(
    Guid CategoryId,
    string Name,
    string Description,
    string Currency,
    decimal Price,
    bool IsPromotionEnabled,
    decimal? PromotionPrice,
    DateTime? PromotionStartUtc,
    DateTime? PromotionEndUtc,
    int StockQty,
    bool IsActive,
    string? MainImageUrl,
    decimal? PromotionPercent = null
);
