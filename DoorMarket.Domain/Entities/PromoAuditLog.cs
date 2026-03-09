using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class PromoAuditLog : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }

    public string Source { get; set; } = "";
    public string? PromoCode { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public string Currency { get; set; } = "USD";
    public bool Applied { get; set; }
    public string Message { get; set; } = "";
}
