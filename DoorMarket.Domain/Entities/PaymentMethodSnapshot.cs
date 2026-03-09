using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class PaymentMethodSnapshot : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public string Provider { get; set; } = "";
    public string? CardBrand { get; set; }
    public string? Last4 { get; set; }
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public string? Country { get; set; }
    public string? Funding { get; set; }
    public string? ProviderPaymentIntentId { get; set; }
    public string? ProviderChargeId { get; set; }
}
