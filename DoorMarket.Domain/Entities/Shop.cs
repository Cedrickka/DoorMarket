using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Shop : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = default!;

    public string Name { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string CountryTag { get; set; } = "";   // ex: RDC, SEN, CIV
    public string City { get; set; } = "";
    public decimal DeliveryBaseFeeUsd { get; set; } = 2m;
    public decimal DeliveryPerKmUsd { get; set; } = 0.55m;

    public bool IsVerified { get; set; } = false;

    public List<Product> Products { get; set; } = new();
    public List<ShopReview> Reviews { get; set; } = new();
}
