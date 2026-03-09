using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class BankBalanceSnapshot : AuditableEntity
{
    public DateTime AsOfDateUtc { get; set; }
    public decimal Balance { get; set; }
    public string? Note { get; set; }
}
