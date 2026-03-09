using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DoorMarket.Api.Services;

public interface IAbandonedCartRecoveryService
{
    Task<AbandonedCartRecoveryRunResult> RunOnceAsync(CancellationToken ct);
}

public sealed record AbandonedCartRecoveryRunResult(
    int CandidatesScanned,
    int EventsCreated,
    int RemindersSent,
    int RemindersFailed,
    int AntiSpamSkipped,
    int ConvertedSkipped);

public sealed class AbandonedCartRecoveryService : IAbandonedCartRecoveryService
{
    private readonly DoorMarketDbContext _db;
    private readonly IEmailSender _email;
    private readonly ICartReminderPushSender _push;
    private readonly CartRecoveryOptions _options;
    private readonly IConfiguration _config;
    private readonly ILogger<AbandonedCartRecoveryService> _logger;

    public AbandonedCartRecoveryService(
        DoorMarketDbContext db,
        IEmailSender email,
        ICartReminderPushSender push,
        IOptions<CartRecoveryOptions> options,
        IConfiguration config,
        ILogger<AbandonedCartRecoveryService> logger)
    {
        _db = db;
        _email = email;
        _push = push;
        _options = options.Value;
        _config = config;
        _logger = logger;
    }

    public async Task<AbandonedCartRecoveryRunResult> RunOnceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleAfterMinutes = Math.Clamp(_options.StaleAfterMinutes, 15, 7 * 24 * 60);
        var antiSpamWindowMinutes = Math.Clamp(_options.AntiSpamWindowMinutes, 30, 30 * 24 * 60);
        var holdoutPercent = Math.Clamp(_options.HoldoutPercent, 0, 90);
        var scanBatchSize = Math.Clamp(_options.ScanBatchSize, 1, 1000);
        var maxNotificationsPerRun = Math.Clamp(_options.MaxNotificationsPerRun, 1, scanBatchSize);

        var staleThreshold = now.AddMinutes(-staleAfterMinutes);
        var antiSpamThreshold = now.AddMinutes(-antiSpamWindowMinutes);

        var cartActivityRows = await _db.CartItems.AsNoTracking()
            .GroupBy(x => x.CartId)
            .Select(g => new
            {
                CartId = g.Key,
                LastActivityAtUtc = g.Max(x => x.UpdatedAtUtc ?? x.CreatedAtUtc),
                ItemCount = g.Sum(x => x.Qty)
            })
            .ToListAsync(ct);

        var staleActivityRows = cartActivityRows
            .Where(x => x.LastActivityAtUtc <= staleThreshold)
            .OrderBy(x => x.LastActivityAtUtc)
            .Take(scanBatchSize)
            .ToList();

        var staleCartIds = staleActivityRows.Select(x => x.CartId).Distinct().ToList();
        var userByCartId = await _db.Carts.AsNoTracking()
            .Where(x => staleCartIds.Contains(x.Id))
            .Select(x => new { x.Id, x.UserId })
            .ToDictionaryAsync(x => x.Id, x => x.UserId, ct);

        var candidateRows = staleActivityRows
            .Where(x => userByCartId.ContainsKey(x.CartId))
            .Select(x => new CandidateRow(
                x.CartId,
                userByCartId[x.CartId],
                x.LastActivityAtUtc,
                x.ItemCount))
            .ToList();

        if (candidateRows.Count == 0)
        {
            return new AbandonedCartRecoveryRunResult(0, 0, 0, 0, 0, 0);
        }

        var userIds = candidateRows.Select(x => x.UserId).Distinct().ToList();
        var cartIds = candidateRows.Select(x => x.CartId).Distinct().ToList();
        var subtotalRows = await _db.CartItems.AsNoTracking()
            .Where(x => cartIds.Contains(x.CartId))
            .Select(x => new
            {
                x.CartId,
                x.UnitPrice,
                x.Qty
            })
            .ToListAsync(ct);
        var subtotalByCartId = subtotalRows
            .GroupBy(x => x.CartId)
            .ToDictionary(
                x => x.Key,
                x => decimal.Round(
                    x.Sum(v => v.UnitPrice * v.Qty),
                    2,
                    MidpointRounding.AwayFromZero));

        var emailByUserId = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);

        var currencyRows = await _db.CartItems.AsNoTracking()
            .Where(x => cartIds.Contains(x.CartId))
            .Select(x => new
            {
                x.CartId,
                Currency = x.Product.Currency
            })
            .ToListAsync(ct);

        var currencyByCartId = currencyRows
            .GroupBy(x => x.CartId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var raw = x.Select(v => v.Currency).FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return "USD";
                    }

                    var trimmed = raw.Trim().ToUpperInvariant();
                    return trimmed.Length <= 8 ? trimmed : trimmed[..8];
                });

        var recentEvents = await _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.DetectedAtUtc >= antiSpamThreshold && cartIds.Contains(x.CartId))
            .Select(x => new { x.CartId, x.UserId })
            .Distinct()
            .ToListAsync(ct);

        var recentEventKeys = new HashSet<string>(
            recentEvents.Select(x => BuildPairKey(x.CartId, x.UserId)),
            StringComparer.Ordinal);

        var remindersSent = 0;
        var remindersFailed = 0;
        var antiSpamSkipped = 0;
        var convertedSkipped = 0;
        var eventsCreated = 0;
        var notifiedCount = 0;

        foreach (var candidate in candidateRows)
        {
            if (notifiedCount >= maxNotificationsPerRun)
            {
                break;
            }

            var pairKey = BuildPairKey(candidate.CartId, candidate.UserId);
            if (recentEventKeys.Contains(pairKey))
            {
                antiSpamSkipped++;
                continue;
            }

            var hasConvertedSinceLastActivity = await _db.Orders.AsNoTracking()
                .AnyAsync(
                    x => x.UserId == candidate.UserId &&
                         x.CreatedAtUtc >= candidate.LastActivityAtUtc,
                    ct);

            if (hasConvertedSinceLastActivity)
            {
                convertedSkipped++;
                continue;
            }

            eventsCreated++;
            notifiedCount++;

            var email = emailByUserId.TryGetValue(candidate.UserId, out var existingEmail)
                ? existingEmail
                : null;
            var currency = currencyByCartId.TryGetValue(candidate.CartId, out var existingCurrency)
                ? existingCurrency
                : "USD";

            var eventRow = new AbandonedCartEvent
            {
                UserId = candidate.UserId,
                CartId = candidate.CartId,
                Currency = currency,
                ItemCount = candidate.ItemCount,
                Subtotal = subtotalByCartId.TryGetValue(candidate.CartId, out var subtotal) ? subtotal : 0m,
                LastCartActivityAtUtc = candidate.LastActivityAtUtc,
                DetectedAtUtc = now,
                ExperimentGroup = ResolveExperimentGroup(candidate, holdoutPercent),
                RecipientEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim()
            };

            var sendResult = _options.AbTestEnabled &&
                             string.Equals(eventRow.ExperimentGroup, "B", StringComparison.OrdinalIgnoreCase)
                ? new ReminderSendResult(
                    NotificationEvents.StatusSkipped,
                    0,
                    null,
                    "Holdout group (B): reminder skipped.",
                    null)
                : await SendReminderAsync(eventRow, ct);

            eventRow.ReminderStatus = sendResult.Status;
            eventRow.ReminderAttemptCount = sendResult.AttemptCount;
            eventRow.SentChannels = sendResult.SentChannels;
            eventRow.Error = sendResult.Error;
            eventRow.ReminderSentAtUtc = sendResult.SentAtUtc;
            eventRow.UpdatedAtUtc = DateTime.UtcNow;

            _db.AbandonedCartEvents.Add(eventRow);
            await _db.SaveChangesAsync(ct);

            if (string.Equals(sendResult.Status, "Sent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sendResult.Status, "Partial", StringComparison.OrdinalIgnoreCase))
            {
                remindersSent++;
            }
            else if (string.Equals(sendResult.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                remindersFailed++;
            }
        }

        return new AbandonedCartRecoveryRunResult(
            candidateRows.Count,
            eventsCreated,
            remindersSent,
            remindersFailed,
            antiSpamSkipped,
            convertedSkipped);
    }

    private async Task<ReminderSendResult> SendReminderAsync(AbandonedCartEvent eventRow, CancellationToken ct)
    {
        var channelsSent = new List<string>();
        var errors = new List<string>();
        var attemptCount = 0;
        var now = DateTime.UtcNow;

        var cartUrl = BuildCartUrl();
        var supportUrl = BuildSupportUrl(eventRow.CartId);
        var subtotalText = string.Format(
            CultureInfo.CurrentCulture,
            "{0:0.##} {1}",
            eventRow.Subtotal,
            eventRow.Currency);

        if (!string.IsNullOrWhiteSpace(eventRow.RecipientEmail))
        {
            var emailSubject = $"[DoorMarket] Votre panier vous attend ({eventRow.ItemCount} article(s))";
            var emailBody = $"""
<h2>Vous avez laisse des articles dans votre panier</h2>
<ul>
  <li>Articles: {eventRow.ItemCount}</li>
  <li>Sous-total estime: {subtotalText}</li>
  <li>Derniere activite panier: {eventRow.LastCartActivityAtUtc:yyyy-MM-dd HH:mm} UTC</li>
</ul>
<p>Reprendre votre commande: <a href="{cartUrl}">{cartUrl}</a></p>
<p>Besoin d'aide: <a href="{supportUrl}">{supportUrl}</a></p>
""";

            try
            {
                attemptCount++;
                await _email.SendAsync(eventRow.RecipientEmail, emailSubject, emailBody, ct);
                channelsSent.Add(NotificationEvents.ChannelEmail);
                AddNotificationLog(
                    NotificationEvents.CartAbandonedReminderEmail,
                    NotificationEvents.ChannelEmail,
                    eventRow.RecipientEmail,
                    emailSubject,
                    NotificationEvents.StatusSent,
                    null);
            }
            catch (Exception ex)
            {
                errors.Add($"Email: {ex.Message}");
                AddNotificationLog(
                    NotificationEvents.CartAbandonedReminderEmail,
                    NotificationEvents.ChannelEmail,
                    eventRow.RecipientEmail,
                    emailSubject,
                    NotificationEvents.StatusFailed,
                    ex.Message);
            }
        }
        else
        {
            errors.Add("Email recipient missing.");
        }

        var pushTitle = "DoorMarket - panier en attente";
        var pushBody = $"Vous avez {eventRow.ItemCount} article(s) dans votre panier.";
        var pushResult = await _push.SendCartReminderAsync(eventRow.UserId, pushTitle, pushBody, ct);
        if (pushResult.Attempted)
        {
            attemptCount++;
            if (pushResult.Sent)
            {
                channelsSent.Add(NotificationEvents.ChannelPush);
                AddNotificationLog(
                    NotificationEvents.CartAbandonedReminderPush,
                    NotificationEvents.ChannelPush,
                    eventRow.UserId.ToString(),
                    pushTitle,
                    NotificationEvents.StatusSent,
                    null);
            }
            else
            {
                var pushError = string.IsNullOrWhiteSpace(pushResult.Error)
                    ? "Push send failed."
                    : pushResult.Error.Trim();
                errors.Add($"Push: {pushError}");
                AddNotificationLog(
                    NotificationEvents.CartAbandonedReminderPush,
                    NotificationEvents.ChannelPush,
                    eventRow.UserId.ToString(),
                    pushTitle,
                    NotificationEvents.StatusFailed,
                    pushError);
            }
        }

        var hasSent = channelsSent.Count > 0;
        var hasError = errors.Count > 0;
        var status = hasSent
            ? hasError ? "Partial" : "Sent"
            : hasError ? "Failed" : "Skipped";

        var sentChannels = channelsSent.Count == 0 ? null : string.Join(",", channelsSent);
        var errorText = errors.Count == 0 ? null : string.Join(" | ", errors);

        if (string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Abandoned cart reminder sent. cartId={CartId} userId={UserId} channels={Channels}",
                eventRow.CartId,
                eventRow.UserId,
                sentChannels);
        }
        else if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Abandoned cart reminder failed. cartId={CartId} userId={UserId} error={Error}",
                eventRow.CartId,
                eventRow.UserId,
                errorText);
        }

        return new ReminderSendResult(
            status,
            attemptCount,
            sentChannels,
            errorText,
            hasSent ? now : null);
    }

    private void AddNotificationLog(
        string notificationType,
        string channel,
        string recipient,
        string subject,
        string status,
        string? error)
    {
        _db.TransactionalNotificationLogs.Add(new TransactionalNotificationLog
        {
            OrderId = null,
            NotificationType = notificationType,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Status = status,
            Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim(),
            AttemptedAtUtc = DateTime.UtcNow
        });
    }

    private string BuildCartUrl()
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return "/cart";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}cart";
    }

    private string BuildSupportUrl(Guid cartId)
    {
        var webBase = (_config["App:WebBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webBase))
        {
            return $"/support?topic=cart&cartId={cartId}";
        }

        if (!webBase.EndsWith("/", StringComparison.Ordinal))
        {
            webBase += "/";
        }

        return $"{webBase}support?topic=cart&cartId={cartId}";
    }

    private static string BuildPairKey(Guid cartId, Guid userId)
        => $"{cartId:N}:{userId:N}";

    private string ResolveExperimentGroup(CandidateRow candidate, int holdoutPercent)
    {
        if (!_options.AbTestEnabled || holdoutPercent <= 0)
        {
            return "A";
        }

        var hash = HashCode.Combine(
            candidate.UserId,
            candidate.CartId,
            candidate.LastActivityAtUtc.Date);
        var bucket = Math.Abs(hash % 100);
        return bucket < holdoutPercent ? "B" : "A";
    }

    private sealed record CandidateRow(
        Guid CartId,
        Guid UserId,
        DateTime LastActivityAtUtc,
        int ItemCount);

    private sealed record ReminderSendResult(
        string Status,
        int AttemptCount,
        string? SentChannels,
        string? Error,
        DateTime? SentAtUtc);
}
