using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class MarketingBanner : AuditableEntity
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? TargetUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    public string? Language { get; set; } // ex: fr, en
    public string? City { get; set; }
    public string? Zone { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid? ShopId { get; set; }
    public Shop? Shop { get; set; }

    public int SortOrder { get; set; } = 0;
    public int Impressions { get; set; } = 0;
    public int Clicks { get; set; } = 0;
}
