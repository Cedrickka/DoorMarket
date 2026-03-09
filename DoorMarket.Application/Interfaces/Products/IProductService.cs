using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;

namespace DoorMarket.Application.Interfaces.Products;

public interface IProductService
{
    // Public
    Task<PagedResult<ProductDto>> SearchAsync(ProductQuery query, CancellationToken ct);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct);

    // Owner
    Task<ProductDto> CreateMyProductAsync(ProductCreateRequest req, CancellationToken ct);
    Task<ProductDto> UpdateMyProductAsync(Guid productId, ProductUpdateRequest req, CancellationToken ct);
    Task<PagedResult<ProductDto>> GetMyProductsAsync(int page, int pageSize, CancellationToken ct);

    Task SetActiveAsync(Guid productId, bool isActive, CancellationToken ct); // owner/admin
    Task AdjustStockAsync(Guid productId, int newStockQty, CancellationToken ct); // owner
}
