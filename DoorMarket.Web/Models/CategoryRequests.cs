namespace DoorMarket.Web.Models;

public record CategoryCreateRequest(string Name, string Slug, string? NameEn = null);
public record CategoryUpdateRequest(string Name, string Slug, string? NameEn = null);
