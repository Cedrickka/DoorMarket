using DoorMarket.Application.DTOs.Categories;

namespace DoorMarket.Mobile.Services;

public sealed class CategoriesApiClient : ApiClientBase
{
    public CategoriesApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<CategoryDto>> GetAllAsync(CancellationToken ct)
        => GetAsync<List<CategoryDto>>("api/categories", ct);

    public Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken ct)
        => GetAsync<CategoryDto>($"api/categories/{id}", ct);
}
