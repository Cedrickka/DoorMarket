using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Application.DTOs.Shops;

namespace DoorMarket.Application.Interfaces.Orders;

public interface IOrderService
{
    Task<OrderDto> CheckoutAsync(CheckoutRequest req, CancellationToken ct);

    Task<PagedResult<OrderDto>> GetMyOrdersAsync(int page, int pageSize, CancellationToken ct);
    Task<OrderDto?> GetMyOrderByIdAsync(Guid id, CancellationToken ct);

    // Shop
    Task<PagedResult<OrderDto>> GetMyShopOrdersAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task SetStatusForMyShopAsync(Guid orderId, string status, CancellationToken ct);
    Task SetFulfillmentStatusForMyShopAsync(Guid orderId, string status, string? note, CancellationToken ct);
    Task<ShopOrderDetailDto?> GetMyShopOrderByIdAsync(Guid orderId, CancellationToken ct);
    Task<ShopDashboardSummaryDto> GetMyShopSummaryAsync(CancellationToken ct);

}
