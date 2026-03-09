using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopReviewHelpfulVote : AuditableEntity
{
    public Guid ShopReviewId { get; set; }
    public ShopReview ShopReview { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public bool IsHelpful { get; set; } = true;
}
