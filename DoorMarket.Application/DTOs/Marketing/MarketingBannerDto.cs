namespace DoorMarket.Application.DTOs.Marketing;

public record MarketingBannerDto(
    Guid Id,
    string Title,
    string? Subtitle,
    string? ImageUrl,
    string? TargetUrl,
    bool IsActive,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string? Language,
    string? City,
    string? Zone,
    Guid? CategoryId,
    Guid? ShopId,
    int SortOrder,
    int Impressions,
    int Clicks,
    DateTime CreatedAtUtc
);

public record CreateMarketingBannerRequest(
    string Title,
    string? Subtitle,
    string? ImageUrl,
    string? TargetUrl,
    bool IsActive,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string? Language,
    string? City,
    string? Zone,
    Guid? CategoryId,
    Guid? ShopId,
    int SortOrder
);

public record UpdateMarketingBannerRequest(
    string Title,
    string? Subtitle,
    string? ImageUrl,
    string? TargetUrl,
    bool IsActive,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    string? Language,
    string? City,
    string? Zone,
    Guid? CategoryId,
    Guid? ShopId,
    int SortOrder
);
