using DoorMarket.Api.Services;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/operations")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminOperationsController : ControllerBase
{
    private const string PayoutStatusDraft = "Draft";
    private const string PayoutStatusApproved = "Approved";
    private const string ApplicationStatusSubmitted = "Submitted";

    private readonly DoorMarketDbContext _db;

    public AdminOperationsController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<OperationsSnapshotDto>> GetSnapshot(
        [FromQuery] int hours = 24,
        [FromQuery] int staleCartHours = 24,
        CancellationToken ct = default)
    {
        hours = Math.Clamp(hours, 1, 24 * 14);
        staleCartHours = Math.Clamp(staleCartHours, 1, 24 * 30);

        var now = DateTime.UtcNow;
        var from = now.AddHours(-hours);
        var staleThreshold = now.AddHours(-staleCartHours);

        var ordersCreated = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= from, ct);
        var ordersPaid = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= from && x.PaymentStatus == "Paid", ct);
        var ordersFailed = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= from && x.PaymentStatus == "Failed", ct);

        var cartUsersActive = await _db.CartItems.AsNoTracking()
            .Where(x => (x.UpdatedAtUtc ?? x.CreatedAtUtc) >= from)
            .Select(x => x.Cart.UserId)
            .Distinct()
            .CountAsync(ct);

        var checkoutUsers = await _db.Orders.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(ct);

        var cartsWithItems = await _db.Carts.AsNoTracking()
            .CountAsync(c => c.Items.Any(), ct);

        var staleCarts = await _db.CartItems.AsNoTracking()
            .GroupBy(x => x.CartId)
            .Select(g => new
            {
                CartId = g.Key,
                LastActivity = g.Max(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            })
            .CountAsync(x => x.LastActivity < staleThreshold, ct);

        var payoutBacklogQuery = _db.ShopPayouts.AsNoTracking()
            .Where(x => x.Status == PayoutStatusDraft || x.Status == PayoutStatusApproved);

        var payoutBacklogCount = await payoutBacklogQuery.CountAsync(ct);
        decimal payoutBacklogAmount;
        var isSqlite = _db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        if (isSqlite)
        {
            payoutBacklogAmount = (await payoutBacklogQuery
                .Select(x => x.NetToPay)
                .ToListAsync(ct))
                .Sum();
        }
        else
        {
            payoutBacklogAmount = await payoutBacklogQuery
                .SumAsync(x => (decimal?)x.NetToPay, ct) ?? 0m;
        }

        var activeProductsOutOfStock = await _db.Products.AsNoTracking()
            .CountAsync(x => x.IsActive && x.StockQty <= 0, ct);
        var activeProductsLowStock = await _db.Products.AsNoTracking()
            .CountAsync(x => x.IsActive && x.StockQty > 0 && x.StockQty <= 5, ct);

        var pendingShopApplications = await _db.ShopApplications.AsNoTracking()
            .CountAsync(x => x.Status == ApplicationStatusSubmitted, ct);

        var liveBanners = await _db.MarketingBanners.AsNoTracking()
            .CountAsync(x =>
                x.IsActive &&
                (!x.StartAtUtc.HasValue || x.StartAtUtc <= now) &&
                (!x.EndAtUtc.HasValue || x.EndAtUtc > now), ct);

        var notificationAttemptsInWindow = await _db.TransactionalNotificationLogs.AsNoTracking()
            .CountAsync(x => x.AttemptedAtUtc >= from, ct);

        var notificationFailedInWindow = await _db.TransactionalNotificationLogs.AsNoTracking()
            .CountAsync(x =>
                x.AttemptedAtUtc >= from &&
                x.Status == NotificationEvents.StatusFailed, ct);

        var notificationFailedUnresolved = await _db.TransactionalNotificationLogs.AsNoTracking()
            .Where(f => f.Status == NotificationEvents.StatusFailed)
            .CountAsync(f => !_db.TransactionalNotificationLogs.Any(s =>
                s.Status == NotificationEvents.StatusSent &&
                s.NotificationType == f.NotificationType &&
                s.Recipient == f.Recipient &&
                ((s.OrderId == f.OrderId) || (!s.OrderId.HasValue && !f.OrderId.HasValue)) &&
                s.AttemptedAtUtc > f.AttemptedAtUtc), ct);

        var checkoutUserConversionRate = cartUsersActive > 0
            ? decimal.Round((decimal)checkoutUsers * 100m / cartUsersActive, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new OperationsSnapshotDto(
            from,
            now,
            ordersCreated,
            ordersPaid,
            ordersFailed,
            cartUsersActive,
            checkoutUsers,
            checkoutUserConversionRate,
            cartsWithItems,
            staleCarts,
            payoutBacklogCount,
            payoutBacklogAmount,
            activeProductsOutOfStock,
            activeProductsLowStock,
            pendingShopApplications,
            liveBanners,
            notificationAttemptsInWindow,
            notificationFailedInWindow,
            notificationFailedUnresolved));
    }

    public sealed record OperationsSnapshotDto(
        DateTime FromUtc,
        DateTime ToUtc,
        int OrdersCreated,
        int OrdersPaid,
        int OrdersFailed,
        int CartUsersActive,
        int CheckoutUsers,
        decimal CheckoutUserConversionRate,
        int CartsWithItems,
        int StaleCarts,
        int PayoutBacklogCount,
        decimal PayoutBacklogAmount,
        int ActiveProductsOutOfStock,
        int ActiveProductsLowStock,
        int PendingShopApplications,
        int LiveBanners,
        int NotificationAttemptsInWindow,
        int NotificationFailedInWindow,
        int NotificationFailedUnresolved);
}
