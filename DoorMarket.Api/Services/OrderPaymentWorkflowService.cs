using DoorMarket.Infrastructure.Persistence;
using DoorMarket.Application.Interfaces.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public interface IOrderPaymentWorkflowService
{
    Task<PaymentWorkflowResult> MarkOrderPaidAsync(
        Guid orderId,
        string paymentProvider,
        PaymentSnapshotInput? snapshot,
        CancellationToken ct);

    Task MarkOrderFailedAsync(Guid orderId, CancellationToken ct);
}

public sealed class OrderPaymentWorkflowService : IOrderPaymentWorkflowService
{
    private readonly DoorMarketDbContext _db;
    private readonly IOrderNotificationService _notifications;
    private readonly IAdminOrderNotificationService _adminNotifications;
    private readonly IClientOrderNotificationService _clientNotifications;
    private readonly ILoyaltyService _loyalty;
    private readonly ILogger<OrderPaymentWorkflowService> _logger;

    public OrderPaymentWorkflowService(
        DoorMarketDbContext db,
        IOrderNotificationService notifications,
        IAdminOrderNotificationService adminNotifications,
        IClientOrderNotificationService clientNotifications,
        ILoyaltyService loyalty,
        ILogger<OrderPaymentWorkflowService> logger)
    {
        _db = db;
        _notifications = notifications;
        _adminNotifications = adminNotifications;
        _clientNotifications = clientNotifications;
        _loyalty = loyalty;
        _logger = logger;
    }

    public async Task<PaymentWorkflowResult> MarkOrderPaidAsync(
        Guid orderId,
        string paymentProvider,
        PaymentSnapshotInput? snapshot,
        CancellationToken ct)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        var order = await _db.Orders
            .AsTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null)
        {
            throw new InvalidOperationException("Commande introuvable.");
        }

        var wasAlreadyFailedBeforeAttempt = string.Equals(order.PaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            var items = await _db.OrderItems
                .AsTracking()
                .Where(i => i.OrderId == orderId)
                .ToListAsync(ct);

            if (items.Count > 0)
            {
                var productIds = items.Select(x => x.ProductId).Distinct().ToList();
                var products = await _db.Products
                    .AsTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);

                foreach (var item in items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product) || product.StockQty < item.Qty)
                    {
                        order.PaymentProvider = paymentProvider;
                        order.PaymentStatus = "Failed";
                        order.UpdatedAtUtc = now;
                        await _db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);
                        if (!wasAlreadyFailedBeforeAttempt)
                        {
                            await NotifyClientPaymentFailedSafeAsync(orderId, "Stock insuffisant.", ct);
                        }
                        return new PaymentWorkflowResult(false, "Failed", "Stock insuffisant.");
                    }
                }

                foreach (var item in items)
                {
                    var product = products[item.ProductId];
                    product.StockQty -= item.Qty;
                    product.UpdatedAtUtc = now;
                }
            }
        }

        var oldFulfillment = order.FulfillmentStatus;
        var wasAlreadyPaid = string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
        order.PaymentProvider = paymentProvider;
        order.PaymentStatus = "Paid";
        order.Status = "Paid";
        if (!wasAlreadyPaid && string.Equals(order.FulfillmentStatus, "PendingPayment", StringComparison.OrdinalIgnoreCase))
        {
            order.FulfillmentStatus = "PaidPending";
        }
        order.PaidAtUtc = now;
        order.UpdatedAtUtc = now;

        if (!wasAlreadyPaid && !string.Equals(oldFulfillment, order.FulfillmentStatus, StringComparison.OrdinalIgnoreCase))
        {
            _db.OrderStatusHistories.Add(new Domain.Entities.OrderStatusHistory
            {
                OrderId = order.Id,
                OldStatus = oldFulfillment,
                NewStatus = order.FulfillmentStatus,
                ChangedByUserId = null,
                ChangedAtUtc = now,
                Note = "Paiement confirme."
            });
        }

        if (snapshot is not null)
        {
            var existingSnapshot = await _db.PaymentMethodSnapshots
                .AsTracking()
                .FirstOrDefaultAsync(x => x.OrderId == order.Id, ct);

            if (existingSnapshot is null)
            {
                existingSnapshot = new Domain.Entities.PaymentMethodSnapshot
                {
                    OrderId = order.Id
                };
                _db.PaymentMethodSnapshots.Add(existingSnapshot);
            }

            existingSnapshot.Provider = paymentProvider;
            existingSnapshot.CardBrand = snapshot.CardBrand;
            existingSnapshot.Last4 = snapshot.Last4;
            existingSnapshot.ExpMonth = snapshot.ExpMonth;
            existingSnapshot.ExpYear = snapshot.ExpYear;
            existingSnapshot.Country = snapshot.Country;
            existingSnapshot.Funding = snapshot.Funding;
            existingSnapshot.ProviderPaymentIntentId = snapshot.ProviderPaymentIntentId;
            existingSnapshot.ProviderChargeId = snapshot.ProviderChargeId;
            existingSnapshot.UpdatedAtUtc = now;
        }

        // Clear cart only once payment is confirmed.
        var cart = await _db.Carts.AsTracking().FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
        if (cart is not null)
        {
            var cartItems = await _db.CartItems.Where(x => x.CartId == cart.Id).ToListAsync(ct);
            _db.CartItems.RemoveRange(cartItems);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (!wasAlreadyPaid)
        {
            try
            {
                await _loyalty.AwardOrderPaidAsync(orderId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Loyalty credit failed. orderId={OrderId}", orderId);
            }
        }

        try
        {
            await _notifications.NotifyShopsOrderPaidPendingOnceAsync(orderId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification shop paid-pending failed. orderId={OrderId}", orderId);
        }

        try
        {
            await _adminNotifications.NotifyAdminOrderPaidOnceAsync(orderId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification admin paid failed. orderId={OrderId}", orderId);
        }

        if (!wasAlreadyPaid)
        {
            try
            {
                await _clientNotifications.NotifyClientPaymentPaidAsync(orderId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Client paid notification failed. orderId={OrderId}", orderId);
            }
        }

        return new PaymentWorkflowResult(true, "Paid", "Paiement confirme.");
    }

    public async Task MarkOrderFailedAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            return;
        }

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(order.PaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        order.PaymentStatus = "Failed";
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await NotifyClientPaymentFailedSafeAsync(orderId, null, ct);
    }

    private async Task NotifyClientPaymentFailedSafeAsync(Guid orderId, string? reason, CancellationToken ct)
    {
        try
        {
            await _clientNotifications.NotifyClientPaymentFailedAsync(orderId, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client failed notification failed. orderId={OrderId}", orderId);
        }
    }
}

public sealed record PaymentSnapshotInput(
    string? CardBrand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    string? Country,
    string? Funding,
    string? ProviderPaymentIntentId,
    string? ProviderChargeId
);

public sealed record PaymentWorkflowResult(bool Paid, string PaymentStatus, string Message);
