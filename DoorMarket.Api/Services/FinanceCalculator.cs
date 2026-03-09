using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public sealed class FinanceCalculator
{
    private readonly DoorMarketDbContext _db;

    public FinanceCalculator(DoorMarketDbContext db)
    {
        _db = db;
    }

    public async Task<FinanceKpis> GetKpisAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);

        var ordersCreated = BuildOrdersCreatedQuery(fromUtc, toUtc);
        var paidOrders = BuildPaidOrdersQuery(fromUtc, toUtc);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);

        var ordersCount = await ordersCreated.CountAsync(ct);
        var paidOrdersCount = await paidOrders.CountAsync(ct);

        var grossSalesItems = paidItems.Sum(i => EffectiveUnitPrice(i) * i.Qty);
        var platformFeeTotal = paidItems.Sum(i => i.PlatformFeeAtPurchase * i.Qty);
        var deliveryRevenue = await paidOrders.SumAsync(o => (decimal?)o.DeliveryFee, ct) ?? 0m;

        var netToShopsTotal = grossSalesItems - platformFeeTotal;
        var platformRevenueTotal = platformFeeTotal + deliveryRevenue;
        var cashInExpected = grossSalesItems + deliveryRevenue;
        var avgBasket = paidOrdersCount <= 0 ? 0m : decimal.Round(grossSalesItems / paidOrdersCount, 2, MidpointRounding.AwayFromZero);

        return new FinanceKpis(
            ordersCount,
            paidOrdersCount,
            grossSalesItems,
            deliveryRevenue,
            platformFeeTotal,
            platformRevenueTotal,
            netToShopsTotal,
            avgBasket,
            cashInExpected
        );
    }

    public async Task<List<TopProductRow>> GetTopProductsAsync(DateTime? from, DateTime? to, int take, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);

        return paidItems
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopProductRow(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(x => x.Qty),
                g.Sum(x => EffectiveUnitPrice(x) * x.Qty),
                g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            ))
            .OrderByDescending(x => x.RevenueItems)
            .ThenByDescending(x => x.QtySold)
            .Take(take)
            .ToList();
    }

    public async Task<List<TopShopRow>> GetTopShopsAsync(DateTime? from, DateTime? to, int take, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);

        return paidItems
            .GroupBy(i => new { i.ShopId, i.ShopName })
            .Select(g => new TopShopRow(
                g.Key.ShopId,
                g.Key.ShopName,
                g.Select(x => x.OrderId).Distinct().Count(),
                g.Sum(x => EffectiveUnitPrice(x) * x.Qty),
                g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            ))
            .OrderByDescending(x => x.GrossSalesItems)
            .ThenByDescending(x => x.OrdersCount)
            .Take(take)
            .ToList();
    }

    public async Task<List<TopCategoryRow>> GetTopCategoriesAsync(DateTime? from, DateTime? to, int take, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);

        return paidItems
            .GroupBy(i => new { i.CategoryId, i.CategoryName })
            .Select(g => new TopCategoryRow(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Sum(x => x.Qty),
                g.Sum(x => EffectiveUnitPrice(x) * x.Qty),
                g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            ))
            .OrderByDescending(x => x.RevenueItems)
            .ThenByDescending(x => x.QtySold)
            .Take(take)
            .ToList();
    }

    public async Task<List<ShopReconciliationRow>> GetShopReconciliationAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);

        return paidItems
            .GroupBy(i => new { i.ShopId, i.ShopName, i.Currency })
            .Select(g => new ShopReconciliationRow(
                g.Key.ShopId,
                g.Key.ShopName,
                g.Key.Currency,
                g.Select(x => x.OrderId).Distinct().Count(),
                g.Sum(x => EffectiveUnitPrice(x) * x.Qty),
                g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            ))
            .OrderByDescending(x => x.GrossSalesItems)
            .ToList();
    }

    public async Task<ShopReconciliationRow?> GetShopTotalsAsync(Guid shopId, string currency, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var paidItems = await LoadPaidItemSnapshotsAsync(fromUtc, toUtc, ct);
        var normalizedCurrency = (currency ?? string.Empty).Trim().ToUpperInvariant();

        return paidItems
            .Where(x => x.ShopId == shopId && string.Equals(x.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            .GroupBy(i => new { i.ShopId, i.ShopName, i.Currency })
            .Select(g => new ShopReconciliationRow(
                g.Key.ShopId,
                g.Key.ShopName,
                g.Key.Currency,
                g.Select(x => x.OrderId).Distinct().Count(),
                g.Sum(x => EffectiveUnitPrice(x) * x.Qty),
                g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            ))
            .FirstOrDefault();
    }

    private IQueryable<Domain.Entities.Order> BuildOrdersCreatedQuery(DateTime? fromUtc, DateTime? toUtc)
    {
        var q = _db.Orders.AsNoTracking();
        if (fromUtc.HasValue)
        {
            q = q.Where(o => o.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            q = q.Where(o => o.CreatedAtUtc < toUtc.Value);
        }

        return q;
    }

    private IQueryable<Domain.Entities.Order> BuildPaidOrdersQuery(DateTime? fromUtc, DateTime? toUtc)
    {
        var q = _db.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == "Paid");

        if (fromUtc.HasValue)
        {
            q = q.Where(o => o.PaidAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            q = q.Where(o => o.PaidAtUtc < toUtc.Value);
        }

        return q;
    }

    private IQueryable<Domain.Entities.OrderItem> BuildPaidOrderItemsQuery(DateTime? fromUtc, DateTime? toUtc)
        => _db.OrderItems.AsNoTracking()
            .Where(i => i.Order.PaymentStatus == "Paid")
            .Where(i => !fromUtc.HasValue || i.Order.PaidAtUtc >= fromUtc.Value)
            .Where(i => !toUtc.HasValue || i.Order.PaidAtUtc < toUtc.Value);

    private async Task<List<PaidItemSnapshot>> LoadPaidItemSnapshotsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        return await BuildPaidOrderItemsQuery(fromUtc, toUtc)
            .Select(i => new PaidItemSnapshot(
                i.ProductId,
                i.Product.Name,
                i.Product.ShopId,
                i.Product.Shop.Name,
                i.Product.CategoryId,
                i.Product.Category.Name,
                i.OrderId,
                i.Order.Currency,
                i.Qty,
                i.UnitPriceAtPurchase,
                i.UnitPrice,
                i.PlatformFeeAtPurchase
            ))
            .ToListAsync(ct);
    }

    private static decimal EffectiveUnitPrice(PaidItemSnapshot item)
        => item.UnitPriceAtPurchase != 0m ? item.UnitPriceAtPurchase : item.UnitPrice;

    private static (DateTime? FromUtc, DateTime? ToUtc) NormalizeRange(DateTime? from, DateTime? to)
    {
        DateTime? fromUtc = from?.ToUniversalTime();
        DateTime? toUtc = to?.ToUniversalTime();

        if (toUtc.HasValue && toUtc.Value.TimeOfDay == TimeSpan.Zero)
        {
            toUtc = toUtc.Value.AddDays(1);
        }

        return (fromUtc, toUtc);
    }

    public sealed record FinanceKpis(
        int OrdersCount,
        int PaidOrdersCount,
        decimal GrossSalesItems,
        decimal DeliveryRevenue,
        decimal PlatformFeeTotal,
        decimal PlatformRevenueTotal,
        decimal NetToShopsTotal,
        decimal AvgBasket,
        decimal CashInExpected);

    public sealed record TopProductRow(Guid ProductId, string Name, int QtySold, decimal RevenueItems, decimal PlatformFee);

    public sealed record TopShopRow(Guid ShopId, string Name, int OrdersCount, decimal GrossSalesItems, decimal PlatformFee)
    {
        public decimal NetToPay => GrossSalesItems - PlatformFee;
    }

    public sealed record TopCategoryRow(Guid CategoryId, string Name, int QtySold, decimal RevenueItems, decimal PlatformFee);

    public sealed record ShopReconciliationRow(Guid ShopId, string Name, string Currency, int OrdersCount, decimal GrossSalesItems, decimal PlatformFee)
    {
        public decimal NetToPay => GrossSalesItems - PlatformFee;
    }

    private sealed record PaidItemSnapshot(
        Guid ProductId,
        string ProductName,
        Guid ShopId,
        string ShopName,
        Guid CategoryId,
        string CategoryName,
        Guid OrderId,
        string Currency,
        int Qty,
        decimal UnitPriceAtPurchase,
        decimal UnitPrice,
        decimal PlatformFeeAtPurchase);
}
