namespace DoorMarket.Mobile.Models;

public sealed class CartLineViewModel
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ImageUrl { get; set; }
}
