using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public interface INotificationReplayService
{
    Task<NotificationReplayResult> RetryAsync(Guid notificationLogId, CancellationToken ct);
}

public sealed class NotificationReplayService : INotificationReplayService
{
    private readonly DoorMarketDbContext _db;
    private readonly IClientOrderNotificationService _clientNotifications;
    private readonly IOrderNotificationService _shopNotifications;
    private readonly IAdminOrderNotificationService _adminNotifications;
    private readonly ILogger<NotificationReplayService> _logger;

    public NotificationReplayService(
        DoorMarketDbContext db,
        IClientOrderNotificationService clientNotifications,
        IOrderNotificationService shopNotifications,
        IAdminOrderNotificationService adminNotifications,
        ILogger<NotificationReplayService> logger)
    {
        _db = db;
        _clientNotifications = clientNotifications;
        _shopNotifications = shopNotifications;
        _adminNotifications = adminNotifications;
        _logger = logger;
    }

    public async Task<NotificationReplayResult> RetryAsync(Guid notificationLogId, CancellationToken ct)
    {
        var log = await _db.TransactionalNotificationLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationLogId, ct);

        if (log is null)
        {
            return new NotificationReplayResult(false, false, "NotFound", "Notification introuvable.", null, null);
        }

        if (!string.Equals(log.Status, NotificationEvents.StatusFailed, StringComparison.OrdinalIgnoreCase))
        {
            return new NotificationReplayResult(true, false, "IgnoredNotFailed", "Seules les notifications en echec peuvent etre rejouees.", log.OrderId, log.NotificationType);
        }

        if (!log.OrderId.HasValue || log.OrderId.Value == Guid.Empty)
        {
            return new NotificationReplayResult(true, false, "MissingOrder", "Impossible de rejouer: OrderId manquant.", log.OrderId, log.NotificationType);
        }

        try
        {
            var orderId = log.OrderId.Value;
            switch (log.NotificationType)
            {
                case NotificationEvents.ClientOrderCreated:
                    await _clientNotifications.NotifyClientOrderCreatedAsync(orderId, ct);
                    break;
                case NotificationEvents.ClientPaymentPaid:
                    await _clientNotifications.NotifyClientPaymentPaidAsync(orderId, ct);
                    break;
                case NotificationEvents.ClientPaymentFailed:
                    await _clientNotifications.NotifyClientPaymentFailedAsync(orderId, "Retry admin", ct);
                    break;
                case NotificationEvents.ShopOrderPaidPending:
                    await _shopNotifications.NotifyShopsOrderPaidPendingOnceAsync(orderId, ct);
                    break;
                case NotificationEvents.AdminOrderPaid:
                    await _adminNotifications.NotifyAdminOrderPaidOnceAsync(orderId, ct);
                    break;
                default:
                    return new NotificationReplayResult(true, false, "UnsupportedType", $"Type non supporte: {log.NotificationType}", log.OrderId, log.NotificationType);
            }

            _logger.LogInformation("Notification replay triggered. logId={LogId} type={Type} orderId={OrderId}",
                log.Id, log.NotificationType, orderId);
            return new NotificationReplayResult(true, true, "Triggered", "Replay declenche.", log.OrderId, log.NotificationType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification replay failed. logId={LogId}", log.Id);
            return new NotificationReplayResult(true, false, "ReplayError", ex.Message, log.OrderId, log.NotificationType);
        }
    }
}

public sealed record NotificationReplayResult(
    bool Found,
    bool Retried,
    string Outcome,
    string Message,
    Guid? OrderId,
    string? NotificationType);
