namespace DoorMarket.Application.DTOs.Shops;

public record ShopReviewDto(
    Guid Id,
    Guid ShopId,
    Guid UserId,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    string? UserEmail,
    bool IsVerifiedPurchase,
    IReadOnlyList<string> PhotoUrls,
    IReadOnlyList<string> VideoUrls,
    int HelpfulCount,
    bool IsHelpfulByCurrentUser
);

public record CreateShopReviewRequest(
    int Rating,
    string? Comment,
    Guid? OrderId,
    IReadOnlyList<string>? PhotoUrls,
    IReadOnlyList<string>? VideoUrls
);

public record VoteShopReviewHelpfulRequest(bool IsHelpful);

public record ShopReviewHelpfulVoteResultDto(
    Guid ReviewId,
    int HelpfulCount,
    bool IsHelpfulByCurrentUser
);

public record ShopReviewSummaryDto(
    Guid ShopId,
    decimal? AverageRating,
    int ReviewCount,
    int Count1,
    int Count2,
    int Count3,
    int Count4,
    int Count5
);
