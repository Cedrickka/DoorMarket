using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class CartItem : AuditableEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }   // snapshot prix au moment ajout
}
