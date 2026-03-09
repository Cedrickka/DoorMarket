using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class MarketingCampaign : AuditableEntity
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Draft"; // Draft | Active | Paused | Completed

    public Guid? SegmentId { get; set; }
    public MarketingSegment? Segment { get; set; }

    public Guid? CouponId { get; set; }
    public Coupon? Coupon { get; set; }

    public bool ChannelEmail { get; set; } = true;
    public bool ChannelPush { get; set; } = false;
    public bool ChannelInApp { get; set; } = false;

    public string? MessageTitle { get; set; }
    public string? MessageBody { get; set; }

    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }

    public List<MarketingCampaignRun> Runs { get; set; } = new();
}
