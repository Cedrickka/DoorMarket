using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopPayout : AuditableEntity
{
    public Guid ShopId { get; set; }
    public Shop Shop { get; set; } = default!;

    public string Currency { get; set; } = "USD";
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    public decimal GrossSalesItems { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal NetToPay { get; set; }
    public decimal DeliveryRevenue { get; set; }

    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Draft"; // Draft -> Approved -> Paid -> Reversed
    public string? IdempotencyKey { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? PaidByUserId { get; set; }
    public DateTime? PaidOutAtUtc { get; set; }
    public Guid? ReversedByUserId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversalReason { get; set; }
    public string? Reference { get; set; }

    public List<ShopPayoutStatusHistory> StatusHistory { get; set; } = new();
}
