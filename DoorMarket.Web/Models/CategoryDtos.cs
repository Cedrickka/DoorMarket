namespace DoorMarket.Web.Models;

public record CategoryDto(Guid Id, string Name, string Slug, string? NameEn = null);
