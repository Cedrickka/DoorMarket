using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class OrderItem : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int Qty { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal UnitPriceAtPurchase { get; set; }
    public decimal PlatformFeeAtPurchase { get; set; }
    public decimal LineTotal { get; set; }
}
