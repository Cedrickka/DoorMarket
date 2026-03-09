using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class UserAddress : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Label { get; set; } = "";        // ✅ ex: Maison / Bureau
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";

    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public Guid? DeliveryZoneId { get; set; }

    public string Street { get; set; } = "";
    public string? Landmark { get; set; }

    public bool IsDefault { get; set; } = false;
}
