using Microsoft.AspNetCore.Components.Forms;

namespace DoorMarket.Web.Models;

public sealed class AdminShopEditModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryTag { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public IBrowserFile? ImageFile { get; set; }
}
