using DoorMarket.Application.DTOs.Categories;

namespace DoorMarket.Application.Interfaces.Categories;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct);
    Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken ct);

    // Admin
    Task<CategoryDto> CreateAsync(CategoryCreateRequest req, CancellationToken ct);
    Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateRequest req, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
