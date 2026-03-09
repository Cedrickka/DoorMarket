using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class DeliveryZone : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "US";
    public string? StateCode { get; set; }
    public decimal FeeUsd { get; set; }
    public bool IsActive { get; set; } = true;
}
