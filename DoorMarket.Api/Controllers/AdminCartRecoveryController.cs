using DoorMarket.Api.Services;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/cart-recovery")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public sealed class AdminCartRecoveryController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly IAbandonedCartRecoveryService _recoveryService;

    public AdminCartRecoveryController(
        DoorMarketDbContext db,
        IAbandonedCartRecoveryService recoveryService)
    {
        _db = db;
        _recoveryService = recoveryService;
    }

    [HttpPost("run-now")]
    public async Task<ActionResult<AbandonedCartRecoveryRunResult>> RunNow(CancellationToken ct = default)
    {
        var run = await _recoveryService.RunOnceAsync(ct);
        return Ok(run);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<CartRecoverySummaryDto>> GetSummary(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int conversionWindowHours = 24 * 7,
        CancellationToken ct = default)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        conversionWindowHours = Math.Clamp(conversionWindowHours, 1, 24 * 30);
        var conversionWindow = TimeSpan.FromHours(conversionWindowHours);

        var events = await _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.DetectedAtUtc >= range.FromUtc && x.DetectedAtUtc < range.ToUtc)
            .OrderByDescending(x => x.DetectedAtUtc)
            .ToListAsync(ct);

        var detected = events.Count;
        var remindersSent = events.Count(x =>
            string.Equals(x.ReminderStatus, NotificationEvents.StatusSent, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.ReminderStatus, "Partial", StringComparison.OrdinalIgnoreCase));
        var remindersFailed = events.Count(x =>
            string.Equals(x.ReminderStatus, NotificationEvents.StatusFailed, StringComparison.OrdinalIgnoreCase));
        var remindersSkipped = events.Count(x =>
            string.Equals(x.ReminderStatus, NotificationEvents.StatusSkipped, StringComparison.OrdinalIgnoreCase));

        var userIds = events.Select(x => x.UserId).Distinct().ToList();
        var cartIds = events.Select(x => x.CartId).Distinct().ToList();
        var minDetected = events.Count == 0 ? range.FromUtc : events.Min(x => x.DetectedAtUtc);
        var maxDetected = events.Count == 0 ? range.ToUtc : events.Max(x => x.DetectedAtUtc);

        var orders = await _db.Orders.AsNoTracking()
            .Where(x =>
                userIds.Contains(x.UserId) &&
                x.CreatedAtUtc >= minDetected &&
                x.CreatedAtUtc <= maxDetected.Add(conversionWindow))
            .Select(x => new { x.UserId, x.CreatedAtUtc })
            .ToListAsync(ct);

        var latestCartActivity = await _db.CartItems.AsNoTracking()
            .Where(x => cartIds.Contains(x.CartId))
            .GroupBy(x => x.CartId)
            .Select(g => new
            {
                CartId = g.Key,
                LastActivityAtUtc = g.Max(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            })
            .ToDictionaryAsync(x => x.CartId, x => x.LastActivityAtUtc, ct);

        var convertedEvents = 0;
        var resumedEvents = 0;

        var groupAEvents = 0;
        var groupAConverted = 0;
        var groupBEvents = 0;
        var groupBConverted = 0;

        foreach (var evt in events)
        {
            var converted = orders.Any(o =>
                o.UserId == evt.UserId &&
                o.CreatedAtUtc >= evt.DetectedAtUtc &&
                o.CreatedAtUtc <= evt.DetectedAtUtc.Add(conversionWindow));
            if (converted)
            {
                convertedEvents++;
            }

            if (latestCartActivity.TryGetValue(evt.CartId, out var lastActivity) &&
                lastActivity > evt.DetectedAtUtc)
            {
                resumedEvents++;
            }

            var group = NormalizeGroup(evt.ExperimentGroup);
            if (group == "A")
            {
                groupAEvents++;
                if (converted)
                {
                    groupAConverted++;
                }
            }
            else if (group == "B")
            {
                groupBEvents++;
                if (converted)
                {
                    groupBConverted++;
                }
            }
        }

        var conversionRatePct = detected == 0 ? 0m : RoundPct(convertedEvents, detected);
        var resumeRatePct = detected == 0 ? 0m : RoundPct(resumedEvents, detected);
        var groupARatePct = groupAEvents == 0 ? 0m : RoundPct(groupAConverted, groupAEvents);
        var groupBRatePct = groupBEvents == 0 ? 0m : RoundPct(groupBConverted, groupBEvents);
        var upliftPct = decimal.Round(groupARatePct - groupBRatePct, 2, MidpointRounding.AwayFromZero);

        var runs = await _db.CartRecoveryJobRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= range.FromUtc && x.StartedAtUtc < range.ToUtc)
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(ct);
        var p50Ms = Percentile(runs.Select(x => x.DurationMs).ToList(), 50);
        var p95Ms = Percentile(runs.Select(x => x.DurationMs).ToList(), 95);
        var runSuccessCount = runs.Count(x => x.Success);
        var runFailureCount = runs.Count(x => !x.Success);

        var smtpFailures = await _db.TransactionalNotificationLogs.AsNoTracking()
            .CountAsync(x =>
                x.AttemptedAtUtc >= range.FromUtc &&
                x.AttemptedAtUtc < range.ToUtc &&
                x.Channel == NotificationEvents.ChannelEmail &&
                x.Status == NotificationEvents.StatusFailed &&
                x.NotificationType == NotificationEvents.CartAbandonedReminderEmail, ct);

        var pushFailures = await _db.TransactionalNotificationLogs.AsNoTracking()
            .CountAsync(x =>
                x.AttemptedAtUtc >= range.FromUtc &&
                x.AttemptedAtUtc < range.ToUtc &&
                x.Channel == NotificationEvents.ChannelPush &&
                x.Status == NotificationEvents.StatusFailed &&
                x.NotificationType == NotificationEvents.CartAbandonedReminderPush, ct);

        return Ok(new CartRecoverySummaryDto(
            range.FromUtc,
            range.ToUtc,
            detected,
            remindersSent,
            remindersFailed,
            remindersSkipped,
            convertedEvents,
            conversionRatePct,
            resumedEvents,
            resumeRatePct,
            groupAEvents,
            groupAConverted,
            groupARatePct,
            groupBEvents,
            groupBConverted,
            groupBRatePct,
            upliftPct,
            runs.Count,
            runSuccessCount,
            runFailureCount,
            p50Ms,
            p95Ms,
            smtpFailures,
            pushFailures));
    }

    [HttpGet("events")]
    public async Task<ActionResult<CartRecoveryEventsPageDto>> GetEvents(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? status = null,
        [FromQuery] string? group = null,
        [FromQuery] int conversionWindowHours = 24 * 7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        conversionWindowHours = Math.Clamp(conversionWindowHours, 1, 24 * 30);
        var conversionWindow = TimeSpan.FromHours(conversionWindowHours);
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var normalizedStatus = Normalize(status);
        var normalizedGroup = Normalize(group);

        var query = _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.DetectedAtUtc >= range.FromUtc && x.DetectedAtUtc < range.ToUtc);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(x => x.ReminderStatus == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(normalizedGroup))
        {
            query = query.Where(x => x.ExperimentGroup == normalizedGroup);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.DetectedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = rows.Select(x => x.UserId).Distinct().ToList();
        var cartIds = rows.Select(x => x.CartId).Distinct().ToList();
        var minDetected = rows.Count == 0 ? range.FromUtc : rows.Min(x => x.DetectedAtUtc);
        var maxDetected = rows.Count == 0 ? range.ToUtc : rows.Max(x => x.DetectedAtUtc);

        var orders = await _db.Orders.AsNoTracking()
            .Where(x =>
                userIds.Contains(x.UserId) &&
                x.CreatedAtUtc >= minDetected &&
                x.CreatedAtUtc <= maxDetected.Add(conversionWindow))
            .Select(x => new { x.UserId, x.CreatedAtUtc })
            .ToListAsync(ct);

        var latestCartActivity = await _db.CartItems.AsNoTracking()
            .Where(x => cartIds.Contains(x.CartId))
            .GroupBy(x => x.CartId)
            .Select(g => new
            {
                CartId = g.Key,
                LastActivityAtUtc = g.Max(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            })
            .ToDictionaryAsync(x => x.CartId, x => x.LastActivityAtUtc, ct);

        var items = rows.Select(evt =>
        {
            var converted = orders.Any(o =>
                o.UserId == evt.UserId &&
                o.CreatedAtUtc >= evt.DetectedAtUtc &&
                o.CreatedAtUtc <= evt.DetectedAtUtc.Add(conversionWindow));
            var resumed = latestCartActivity.TryGetValue(evt.CartId, out var lastActivity) &&
                          lastActivity > evt.DetectedAtUtc;

            return new CartRecoveryEventRowDto(
                evt.Id,
                evt.UserId,
                evt.CartId,
                evt.ExperimentGroup,
                evt.ReminderStatus,
                evt.ItemCount,
                evt.Subtotal,
                evt.Currency,
                evt.DetectedAtUtc,
                evt.LastCartActivityAtUtc,
                evt.ReminderSentAtUtc,
                evt.SentChannels,
                evt.ReminderAttemptCount,
                evt.Error,
                resumed,
                converted);
        }).ToList();

        return Ok(new CartRecoveryEventsPageDto(page, pageSize, total, items));
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = toUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var from = fromUtc?.ToUniversalTime() ?? to.AddDays(-30);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return (from, to);
    }

    private static decimal RoundPct(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
    }

    private static int? Percentile(List<int> values, int percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var p = Math.Clamp(percentile, 0, 100) / 100d;
        var index = (int)Math.Ceiling(values.Count * p) - 1;
        if (index < 0)
        {
            index = 0;
        }

        if (index >= values.Count)
        {
            index = values.Count - 1;
        }

        return values[index];
    }

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static string NormalizeGroup(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "B" => "B",
            _ => "A"
        };
    }

    public sealed record CartRecoverySummaryDto(
        DateTime FromUtc,
        DateTime ToUtc,
        int DetectedEvents,
        int RemindersSent,
        int RemindersFailed,
        int RemindersSkipped,
        int ConvertedEvents,
        decimal ConversionRatePct,
        int ResumedEvents,
        decimal ResumeRatePct,
        int GroupAEvents,
        int GroupAConverted,
        decimal GroupAConversionRatePct,
        int GroupBEvents,
        int GroupBConverted,
        decimal GroupBConversionRatePct,
        decimal UpliftPct,
        int JobRunsCount,
        int JobRunsSuccessCount,
        int JobRunsFailureCount,
        int? JobLatencyP50Ms,
        int? JobLatencyP95Ms,
        int SmtpFailures,
        int PushFailures);

    public sealed record CartRecoveryEventsPageDto(
        int Page,
        int PageSize,
        int Total,
        IReadOnlyList<CartRecoveryEventRowDto> Items);

    public sealed record CartRecoveryEventRowDto(
        Guid Id,
        Guid UserId,
        Guid CartId,
        string ExperimentGroup,
        string ReminderStatus,
        int ItemCount,
        decimal Subtotal,
        string Currency,
        DateTime DetectedAtUtc,
        DateTime LastCartActivityAtUtc,
        DateTime? ReminderSentAtUtc,
        string? SentChannels,
        int ReminderAttemptCount,
        string? Error,
        bool Resumed,
        bool Converted);
}

