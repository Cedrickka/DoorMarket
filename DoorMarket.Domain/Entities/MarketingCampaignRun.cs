using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class MarketingCampaignRun : AuditableEntity
{
    public Guid CampaignId { get; set; }
    public MarketingCampaign Campaign { get; set; } = default!;

    public string RunType { get; set; } = "Manual"; // Manual | Scheduled
    public string Status { get; set; } = "Completed"; // Running | Completed | Failed | Partial

    public int TargetUsers { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public decimal RevenueAttributed { get; set; }
    public decimal DiscountCost { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
}
