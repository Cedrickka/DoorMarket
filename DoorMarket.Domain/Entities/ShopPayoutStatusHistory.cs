using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopPayoutStatusHistory : AuditableEntity
{
    public Guid ShopPayoutId { get; set; }
    public ShopPayout ShopPayout { get; set; } = default!;

    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
}
