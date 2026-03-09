using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DoorMarket.Api.Services;

public interface IAdminOrderNotificationService
{
    Task NotifyAdminOrderPaidOnceAsync(Guid orderId, CancellationToken ct);
}

public sealed class AdminOrderNotificationService : IAdminOrderNotificationService
{
    private readonly DoorMarketDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<AdminOrderNotificationService> _logger;
    private readonly IConfiguration _config;

    public AdminOrderNotificationService(
        DoorMarketDbContext db,
        IEmailSender email,
        ILogger<AdminOrderNotificationService> logger,
        IConfiguration config)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _config = config;
    }

    public async Task NotifyAdminOrderPaidOnceAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null)
        {
            return;
        }

        if (order.AdminNotifiedPaid)
        {
            return;
        }

        var adminEmail = (_config["Admin:Email"] ?? "admin@door-market.com").Trim();
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var items = await _db.OrderItems.AsNoTracking()
            .Include(i => i.Product)
            .Where(i => i.OrderId == orderId)
            .ToListAsync(ct);

        var shopsCount = items.Select(i => i.Product.ShopId).Distinct().Count();
        var totalItems = items.Sum(i => i.Qty);
        var platformFeeTotal = order.PlatformFeeTotal;

        if (platformFeeTotal <= 0m)
        {
            platformFeeTotal = items.Sum(i => i.PlatformFeeAtPurchase * i.Qty);
        }

        var orderCode = BuildOrderCode(order.Id);
        var totalText = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var subject = $"Commande payee - {orderCode} - {totalText}";
        var body = BuildBody(order, orderCode, totalItems, shopsCount, platformFeeTotal);

        try
        {
            await _email.SendAsync(adminEmail, subject, body, ct);
            _db.TransactionalNotificationLogs.Add(new Domain.Entities.TransactionalNotificationLog
            {
                OrderId = orderId,
                NotificationType = NotificationEvents.AdminOrderPaid,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = adminEmail,
                Subject = subject,
                Status = NotificationEvents.StatusSent,
                AttemptedAtUtc = DateTime.UtcNow
            });
            order.AdminNotifiedPaid = true;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Echec email notification admin. orderId={OrderId}", orderId);
            _db.TransactionalNotificationLogs.Add(new Domain.Entities.TransactionalNotificationLog
            {
                OrderId = orderId,
                NotificationType = NotificationEvents.AdminOrderPaid,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = adminEmail,
                Subject = subject,
                Status = NotificationEvents.StatusFailed,
                Error = ex.Message,
                AttemptedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string BuildOrderCode(Guid orderId)
    {
        var compact = orderId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }

    private string BuildBody(Domain.Entities.Order order, string orderCode, int totalItems, int shopsCount, decimal platformFeeTotal)
    {
        var paidAt = order.PaidAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "N/A";
        var subtotalText = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalItemsAmount > 0m ? order.TotalItemsAmount : order.Subtotal, order.Currency);
        var deliveryText = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.DeliveryFee, order.Currency);
        var platformFeeText = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", platformFeeTotal, order.Currency);
        var totalText = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var adminLink = BuildAdminLink(order.Id);

        return $"""
<h2>Commande payee - {orderCode}</h2>
<ul>
  <li>Commande: {order.Id}</li>
  <li>Client: {order.DeliveryName} ({order.DeliveryPhone})</li>
  <li>Articles: {totalItems}</li>
  <li>Boutiques: {shopsCount}</li>
  <li>Sous-total produits: {subtotalText}</li>
  <li>Livraison: {deliveryText}</li>
  <li>Commission plateforme: {platformFeeText}</li>
  <li>Total: {totalText}</li>
  <li>Paiement: {order.PaymentProvider}</li>
  <li>Date paiement: {paidAt}</li>
</ul>
<p>Admin: <a href=\"{adminLink}\">{adminLink}</a></p>
""";
    }

    private string BuildAdminLink(Guid orderId)
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return $"/admin/orders/{orderId}";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}admin/orders/{orderId}";
    }
}
