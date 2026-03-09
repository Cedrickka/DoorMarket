using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ShopApplicationDocument : AuditableEntity
{
    public Guid ShopApplicationId { get; set; }
    public ShopApplication ShopApplication { get; set; } = default!;

    public string DocType { get; set; } = "";       // ID|RCCM|TAX|OTHER
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }

    public string StoragePath { get; set; } = "";   // interne
    public string PublicUrl { get; set; } = "";     // staging
}
