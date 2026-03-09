namespace DoorMarket.Web.Models;

public class ProductFormModel
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CategoryId { get; set; }
}
