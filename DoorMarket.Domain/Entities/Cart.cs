using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public List<CartItem> Items { get; set; } = new();
}
