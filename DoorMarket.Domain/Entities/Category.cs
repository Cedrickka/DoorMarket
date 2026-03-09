using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Category : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string Slug { get; set; } = "";

    public List<Product> Products { get; set; } = new();
}
