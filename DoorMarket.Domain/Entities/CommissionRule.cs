using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class CommissionRule : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string ScopeType { get; set; } = "Global"; // Global | Shop | Category | Product
    public Guid? ScopeShopId { get; set; }
    public Guid? ScopeCategoryId { get; set; }
    public Guid? ScopeProductId { get; set; }
    public string? Currency { get; set; } // null => all currencies
    public string PlatformFeeMode { get; set; } = "Flat"; // Flat | Percent
    public decimal PlatformFeeAmount { get; set; } = 0m;
    public decimal? PlatformFeePercent { get; set; }
    public decimal? MinUnitPrice { get; set; }
    public decimal? MaxUnitPrice { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
