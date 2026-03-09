using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DoorMarket.Api.Services;

public interface IOrderNotificationService
{
    Task NotifyShopsOrderPaidPendingOnceAsync(Guid orderId, CancellationToken ct);
}

public sealed class OrderNotificationService : IOrderNotificationService
{
    private readonly DoorMarketDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<OrderNotificationService> _logger;
    private readonly IConfiguration _config;

    public OrderNotificationService(DoorMarketDbContext db, IEmailSender email, ILogger<OrderNotificationService> logger, IConfiguration config)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _config = config;
    }

    public async Task NotifyShopsOrderPaidPendingOnceAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);

        if (order is null)
        {
            return;
        }

        if (order.NotifiedShopPaidPendingAt is not null)
        {
            return;
        }

        var notifications = await (
            from oi in _db.OrderItems.AsNoTracking()
            join p in _db.Products.AsNoTracking() on oi.ProductId equals p.Id
            join s in _db.Shops.AsNoTracking() on p.ShopId equals s.Id
            join u in _db.Users.AsNoTracking() on s.OwnerUserId equals u.Id
            where oi.OrderId == orderId && !string.IsNullOrWhiteSpace(u.Email)
            group new { oi, p, s, u } by new { s.Id, s.Name, u.Email } into g
            select new ShopOrderNotification(
                g.Key.Name,
                g.Key.Email!,
                g.Sum(x => x.oi.Qty),
                g.Sum(x => x.oi.LineTotal),
                g.Select(x => $"{x.oi.Qty} x {x.p.Name}").ToList())
        ).ToListAsync(ct);

        if (notifications.Count == 0)
        {
            return;
        }

        var orderCode = BuildOrderCode(order.Id);
        foreach (var item in notifications)
        {
            try
            {
                var subject = $"[DoorMarket] Nouvelle commande payee {orderCode}";
                var body = BuildBody(order, orderCode, item);
                await _email.SendAsync(item.ShopEmail, subject, body, ct);
                _db.TransactionalNotificationLogs.Add(new Domain.Entities.TransactionalNotificationLog
                {
                    OrderId = orderId,
                    NotificationType = NotificationEvents.ShopOrderPaidPending,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = item.ShopEmail,
                    Subject = subject,
                    Status = NotificationEvents.StatusSent,
                    AttemptedAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Echec email notification boutique. shop={ShopName} email={Email} order={OrderId}",
                    item.ShopName, item.ShopEmail, orderId);
                _db.TransactionalNotificationLogs.Add(new Domain.Entities.TransactionalNotificationLog
                {
                    OrderId = orderId,
                    NotificationType = NotificationEvents.ShopOrderPaidPending,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = item.ShopEmail,
                    Subject = $"[DoorMarket] Nouvelle commande payee {orderCode}",
                    Status = NotificationEvents.StatusFailed,
                    Error = ex.Message,
                    AttemptedAtUtc = DateTime.UtcNow
                });
            }
        }

        order.NotifiedShopPaidPendingAt = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string BuildOrderCode(Guid orderId)
    {
        var compact = orderId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }

    private string BuildBody(Domain.Entities.Order order, string orderCode, ShopOrderNotification item)
    {
        var paidAt = order.PaidAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "N/A";
        var totalForShop = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", item.ShopTotalAmount, order.Currency);
        var orderTotal = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var itemsHtml = string.Join("", item.ItemLines.Select(x => $"<li>{x}</li>"));
        var dashboardLink = BuildDashboardLink(order.Id);

        return $"""
<h2>Commande payee - {orderCode}</h2>
<p>Boutique: <strong>{item.ShopName}</strong></p>
<p>La commande est payee. Merci de preparer le colis.</p>
<ul>
  <li>Commande: {order.Id}</li>
  <li>Articles boutique: {item.TotalItems}</li>
  <li>Montant boutique: {totalForShop}</li>
  <li>Total commande: {orderTotal}</li>
  <li>Paiement: {order.PaymentProvider}</li>
  <li>Date paiement: {paidAt}</li>
</ul>
<p>Articles:</p>
<ul>{itemsHtml}</ul>
<p>Livraison: {order.DeliveryName} - {order.DeliveryPhone}</p>
<p>Adresse: {order.DeliveryLine1}, {order.DeliveryCity}, {order.DeliveryCountry}</p>
<p>Dashboard boutique: <a href=\"{dashboardLink}\">{dashboardLink}</a></p>
""";
    }

    private string BuildDashboardLink(Guid orderId)
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return $"/shop/orders/{orderId}";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}shop/orders/{orderId}";
    }

    private sealed record ShopOrderNotification(
        string ShopName,
        string ShopEmail,
        int TotalItems,
        decimal ShopTotalAmount,
        List<string> ItemLines);
}
