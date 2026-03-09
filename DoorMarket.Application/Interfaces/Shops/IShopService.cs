using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Shops;

namespace DoorMarket.Application.Interfaces.Shops;

public interface IShopService
{
    Task<ShopDto> CreateMyShopAsync(ShopCreateRequest req, CancellationToken ct);
    Task<ShopDto?> GetMyShopAsync(CancellationToken ct);
    Task<ShopDto> UpdateMyShopAsync(ShopUpdateRequest req, CancellationToken ct);

    Task<ShopDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResult<ShopDto>> SearchAsync(ShopQuery query, CancellationToken ct);

    // Admin
    Task<PagedResult<ShopDto>> AdminSearchAsync(ShopQuery query, CancellationToken ct);
    Task VerifyAsync(Guid shopId, CancellationToken ct);
    Task<ShopDto> AdminUpdateAsync(Guid shopId, ShopUpdateRequest req, CancellationToken ct);
    Task AdminDeleteAsync(Guid shopId, CancellationToken ct);

    Task<ShopDashboardSummaryDto> GetMySummaryAsync(CancellationToken ct);
}
