using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopApplication : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = default!;

    public string Name { get; set; } = "";           // nom boutique demandé
    public string? ImageUrl { get; set; }
    public string CountryTag { get; set; } = "";     // RDC, SEN...
    public string City { get; set; } = "";

    public string Status { get; set; } = "Draft";    // Draft|Submitted|Approved|Rejected

    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public Guid? ShopId { get; set; }
    public Shop? Shop { get; set; }

    public List<ShopApplicationDocument> Documents { get; set; } = new();
}
