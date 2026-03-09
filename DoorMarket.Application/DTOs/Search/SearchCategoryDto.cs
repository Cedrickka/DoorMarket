namespace DoorMarket.Application.DTOs.Search;

public record SearchCategoryDto(
    Guid Id,
    string Name,
    string? NameEn,
    string Slug,
    int ActiveProductsCount,
    int ActiveShopsCount);
