namespace DoorMarket.Application.DTOs.Categories;

public record CategoryUpdateRequest(
    string Name,
    string Slug,
    string? NameEn = null
);
