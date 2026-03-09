namespace DoorMarket.Web.Models;

public record ShopDashboardSummaryDto(
    int OrdersToday,
    decimal RevenueToday,
    int ProductsActive,
    int ProductsOutOfStock
);
