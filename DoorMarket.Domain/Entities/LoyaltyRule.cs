using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class LoyaltyRule : AuditableEntity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;

    // Earning
    public decimal EarnPointsPerUsd { get; set; } = 1m;
    public decimal? MinOrderAmountUsd { get; set; }

    // Redeem
    public decimal RedeemValueUsdPerPoint { get; set; } = 0.01m;
    public int MinRedeemPoints { get; set; } = 100;
    public decimal MaxRedeemPercentOfOrder { get; set; } = 25m;

    public string? Notes { get; set; }
}
