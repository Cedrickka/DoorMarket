using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class LoyaltyPointLedger : AuditableEntity
{
    public Guid WalletId { get; set; }
    public LoyaltyWallet Wallet { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    // Earn | Redeem | Adjust | Expire
    public string EntryType { get; set; } = "Earn";
    public int DeltaPoints { get; set; }
    public decimal DeltaAmount { get; set; }
    public int BalanceAfterPoints { get; set; }
    public decimal BalanceAfterAmount { get; set; }

    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? Note { get; set; }
}
