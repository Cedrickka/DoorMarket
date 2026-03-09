using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class SearchQueryRule : AuditableEntity
{
    public string TriggerQuery { get; set; } = "";
    public string? CanonicalQuery { get; set; }
    public string? TargetType { get; set; } // Product | Shop | Category
    public Guid? TargetId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}
