using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class OrderStatusHistory : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
