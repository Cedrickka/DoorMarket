namespace DoorMarket.Application.DTOs.Categories;

public record CategoryCreateRequest(
    string Name,
    string Slug,
    string? NameEn = null
);
