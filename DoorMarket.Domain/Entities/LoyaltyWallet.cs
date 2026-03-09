using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class LoyaltyWallet : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public int PointsBalance { get; set; }
    public int LifetimePointsEarned { get; set; }
    public int LifetimePointsSpent { get; set; }

    public decimal MonetaryBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? LastActivityAtUtc { get; set; }

    public List<LoyaltyPointLedger> Ledger { get; set; } = new();
}
