using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Percent | Fixed
    public string DiscountType { get; set; } = "Percent";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public string? Currency { get; set; }
    public decimal? MinSubtotal { get; set; }

    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    // Total discount budget (sum of applied discounts).
    public decimal? BudgetAmount { get; set; }

    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerUser { get; set; }

    // Global | Shop | Category | City | Country
    public string ScopeType { get; set; } = "Global";
    public Guid? ScopeShopId { get; set; }
    public Guid? ScopeCategoryId { get; set; }
    public string? ScopeCity { get; set; }
    public string? ScopeCountry { get; set; }
}
