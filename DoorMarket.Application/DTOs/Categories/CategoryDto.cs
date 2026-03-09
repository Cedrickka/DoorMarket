namespace DoorMarket.Application.DTOs.Categories;

public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    DateTime CreatedAtUtc,
    string? NameEn = null
);
