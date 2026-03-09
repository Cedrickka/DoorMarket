namespace DoorMarket.Application.DTOs.Shops;

public record ShopDashboardSummaryDto(
    int OrdersToday,
    decimal RevenueToday,
    int ProductsActive,
    int ProductsOutOfStock
);
