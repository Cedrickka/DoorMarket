using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopReview : AuditableEntity
{
    public Guid ShopId { get; set; }
    public Shop Shop { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid? OrderId { get; set; }
    public bool IsVerifiedPurchase { get; set; }

    public int Rating { get; set; } // 1..5
    public string? Comment { get; set; }
    public string? PhotoUrlsJson { get; set; }
    public string? VideoUrlsJson { get; set; }
    public int HelpfulCount { get; set; } = 0;

    public List<ShopReviewHelpfulVote> HelpfulVotes { get; set; } = new();
}
