using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class MarketingSegment : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string CriteriaJson { get; set; } = "{}";
    public bool IsSystem { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
