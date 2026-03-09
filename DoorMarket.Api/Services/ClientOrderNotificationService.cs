using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DoorMarket.Api.Services;

public interface IClientOrderNotificationService
{
    Task NotifyClientOrderCreatedAsync(Guid orderId, CancellationToken ct);
    Task NotifyClientPaymentPaidAsync(Guid orderId, CancellationToken ct);
    Task NotifyClientPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct);
}

public sealed class ClientOrderNotificationService : IClientOrderNotificationService
{
    private readonly DoorMarketDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<ClientOrderNotificationService> _logger;
    private readonly IConfiguration _config;

    public ClientOrderNotificationService(
        DoorMarketDbContext db,
        IEmailSender email,
        ILogger<ClientOrderNotificationService> logger,
        IConfiguration config)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _config = config;
    }

    public async Task NotifyClientOrderCreatedAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null || string.IsNullOrWhiteSpace(order.ClientEmail))
        {
            return;
        }

        var orderCode = BuildOrderCode(order.Id);
        var totalItems = await CountOrderItemsAsync(order.Id, ct);
        var subject = $"[DoorMarket] Commande recue {orderCode} - paiement en attente";
        var body = BuildCreatedBody(order, orderCode, totalItems);

        await SendSafeAsync(order.ClientEmail, subject, body, NotificationEvents.ClientOrderCreated, order.Id, ct);
    }

    public async Task NotifyClientPaymentPaidAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null ||
            string.IsNullOrWhiteSpace(order.ClientEmail) ||
            !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var orderCode = BuildOrderCode(order.Id);
        var totalItems = await CountOrderItemsAsync(order.Id, ct);
        var subject = $"[DoorMarket] Paiement confirme {orderCode}";
        var body = BuildPaidBody(order, orderCode, totalItems);

        await SendSafeAsync(order.ClientEmail, subject, body, NotificationEvents.ClientPaymentPaid, order.Id, ct);
    }

    public async Task NotifyClientPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null ||
            string.IsNullOrWhiteSpace(order.ClientEmail) ||
            !string.Equals(order.PaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var orderCode = BuildOrderCode(order.Id);
        var subject = $"[DoorMarket] Paiement echoue {orderCode}";
        var body = BuildFailedBody(order, orderCode, reason);

        await SendSafeAsync(order.ClientEmail, subject, body, NotificationEvents.ClientPaymentFailed, order.Id, ct);
    }

    private async Task SendSafeAsync(string email, string subject, string body, string notificationType, Guid orderId, CancellationToken ct)
    {
        string status;
        string? error = null;

        try
        {
            await _email.SendAsync(email, subject, body, ct);
            status = NotificationEvents.StatusSent;
            _logger.LogInformation("Client transactional notification sent. type={Type} orderId={OrderId} email={Email}", notificationType, orderId, email);
        }
        catch (Exception ex)
        {
            status = NotificationEvents.StatusFailed;
            error = ex.Message;
            _logger.LogWarning(ex, "Client transactional notification failed. type={Type} orderId={OrderId} email={Email}", notificationType, orderId, email);
        }

        await SaveLogAsync(orderId, notificationType, email, subject, status, error, ct);
    }

    private async Task SaveLogAsync(
        Guid orderId,
        string notificationType,
        string recipient,
        string subject,
        string status,
        string? error,
        CancellationToken ct)
    {
        _db.TransactionalNotificationLogs.Add(new Domain.Entities.TransactionalNotificationLog
        {
            OrderId = orderId,
            NotificationType = notificationType,
            Channel = NotificationEvents.ChannelEmail,
            Recipient = recipient,
            Subject = subject,
            Status = status,
            Error = error,
            AttemptedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int> CountOrderItemsAsync(Guid orderId, CancellationToken ct)
    {
        return await _db.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .SumAsync(x => (int?)x.Qty, ct) ?? 0;
    }

    private async Task<ClientOrderNotificationPayload?> LoadOrderAsync(Guid orderId, CancellationToken ct)
    {
        return await _db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new ClientOrderNotificationPayload(
                x.Id,
                x.User.Email,
                x.DeliveryName,
                x.DeliveryCity,
                x.DeliveryCountry,
                x.PaymentProvider,
                x.PaymentStatus,
                x.FulfillmentStatus,
                x.TotalAmount,
                x.Currency,
                x.CreatedAtUtc,
                x.PaidAtUtc))
            .FirstOrDefaultAsync(ct);
    }

    private string BuildCreatedBody(ClientOrderNotificationPayload order, string orderCode, int totalItems)
    {
        var total = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var createdAt = order.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        var orderLink = BuildOrderLink(order.Id);

        return $"""
<h2>Commande enregistree - {orderCode}</h2>
<p>Bonjour {order.DeliveryName},</p>
<p>Votre commande est bien enregistree et attend la confirmation de paiement.</p>
<ul>
  <li>Commande: {order.Id}</li>
  <li>Articles: {totalItems}</li>
  <li>Total: {total}</li>
  <li>Paiement: {order.PaymentProvider}</li>
  <li>Statut paiement: {order.PaymentStatus}</li>
  <li>Date creation: {createdAt}</li>
</ul>
<p>Adresse livraison: {order.DeliveryCity}, {order.DeliveryCountry}</p>
<p>Suivre la commande: <a href=\"{orderLink}\">{orderLink}</a></p>
""";
    }

    private string BuildPaidBody(ClientOrderNotificationPayload order, string orderCode, int totalItems)
    {
        var total = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var paidAt = order.PaidAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "N/A";
        var orderLink = BuildOrderLink(order.Id);

        return $"""
<h2>Paiement confirme - {orderCode}</h2>
<p>Bonjour {order.DeliveryName},</p>
<p>Nous avons confirme votre paiement. Votre commande passe en preparation.</p>
<ul>
  <li>Commande: {order.Id}</li>
  <li>Articles: {totalItems}</li>
  <li>Total paye: {total}</li>
  <li>Methode de paiement: {order.PaymentProvider}</li>
  <li>Date paiement: {paidAt}</li>
  <li>Statut commande: {order.FulfillmentStatus}</li>
</ul>
<p>Suivre la commande: <a href=\"{orderLink}\">{orderLink}</a></p>
""";
    }

    private string BuildFailedBody(ClientOrderNotificationPayload order, string orderCode, string? reason)
    {
        var total = string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", order.TotalAmount, order.Currency);
        var orderLink = BuildOrderLink(order.Id);
        var supportLink = BuildSupportLink(order.Id);
        var detail = string.IsNullOrWhiteSpace(reason) ? "Paiement non confirme." : reason.Trim();

        return $"""
<h2>Paiement echoue - {orderCode}</h2>
<p>Bonjour {order.DeliveryName},</p>
<p>Le paiement de votre commande n'a pas pu etre confirme.</p>
<ul>
  <li>Commande: {order.Id}</li>
  <li>Montant: {total}</li>
  <li>Methode de paiement: {order.PaymentProvider}</li>
  <li>Raison: {detail}</li>
</ul>
<p>Reessayer le paiement: <a href=\"{orderLink}\">{orderLink}</a></p>
<p>Besoin d'aide: <a href=\"{supportLink}\">{supportLink}</a></p>
""";
    }

    private string BuildOrderLink(Guid orderId)
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return $"/orders/{orderId}";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}orders/{orderId}";
    }

    private string BuildSupportLink(Guid orderId)
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return $"/support?orderId={orderId}";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}support?orderId={orderId}";
    }

    private static string BuildOrderCode(Guid orderId)
    {
        var compact = orderId.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return $"DM{compact[..6]}";
    }

    private sealed record ClientOrderNotificationPayload(
        Guid Id,
        string ClientEmail,
        string DeliveryName,
        string DeliveryCity,
        string DeliveryCountry,
        string PaymentProvider,
        string PaymentStatus,
        string FulfillmentStatus,
        decimal TotalAmount,
        string Currency,
        DateTime CreatedAtUtc,
        DateTime? PaidAtUtc);
}
