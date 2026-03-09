using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public sealed class ProductionObservabilityDashboardService
{
    private readonly DoorMarketDbContext _db;
    private readonly CheckoutObservabilityCalculator _checkoutCalculator;

    public ProductionObservabilityDashboardService(
        DoorMarketDbContext db,
        CheckoutObservabilityCalculator checkoutCalculator)
    {
        _db = db;
        _checkoutCalculator = checkoutCalculator;
    }

    public async Task<ProductionDashboardDto> GetDashboardAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var normalizedFromUtc = fromUtc.Kind == DateTimeKind.Utc ? fromUtc : fromUtc.ToUniversalTime();
        var normalizedToUtc = toUtc.Kind == DateTimeKind.Utc ? toUtc : toUtc.ToUniversalTime();
        if (normalizedFromUtc >= normalizedToUtc)
        {
            normalizedFromUtc = normalizedToUtc.AddHours(-4);
        }

        // IMPORTANT:
        // Use sequential EF operations on the same scoped DbContext.
        // Parallel async queries (Task.WhenAll) on a single context trigger:
        // "A second operation was started on this context instance..."
        var snapshot = await _checkoutCalculator.GetSnapshotAsync(normalizedFromUtc, normalizedToUtc, ct);

        var checkoutEvents = await _db.CheckoutAnalyticsEvents.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= normalizedFromUtc && x.OccurredAtUtc < normalizedToUtc)
            .Select(x => new CheckoutEventErrorRow(x.EventType, x.Success, x.ErrorCode, x.ErrorMessage, x.OccurredAtUtc))
            .ToListAsync(ct);

        var cartRuns = await _db.CartRecoveryJobRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= normalizedFromUtc && x.StartedAtUtc < normalizedToUtc)
            .Select(x => new CartJobRunRow(x.StartedAtUtc, x.EndedAtUtc, x.DurationMs, x.Success, x.Error))
            .ToListAsync(ct);

        var reconciliationRuns = await _db.ReconciliationJobRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= normalizedFromUtc && x.StartedAtUtc < normalizedToUtc)
            .Select(x => new ReconciliationJobRunRow(x.StartedAtUtc, x.EndedAtUtc, x.DurationMs, x.Success, x.InProgress, x.Error))
            .ToListAsync(ct);

        var marketingRuns = await _db.MarketingCampaignRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= normalizedFromUtc && x.StartedAtUtc < normalizedToUtc)
            .Select(x => new MarketingJobRunRow(x.StartedAtUtc, x.CompletedAtUtc, x.Status, x.FailedCount, x.Notes))
            .ToListAsync(ct);

        var abandonedRows = await _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.DetectedAtUtc >= normalizedFromUtc && x.DetectedAtUtc < normalizedToUtc)
            .Select(x => new AbandonedQueueRow(x.ReminderStatus, x.DetectedAtUtc))
            .ToListAsync(ct);

        var notificationRows = await _db.TransactionalNotificationLogs.AsNoTracking()
            .Where(x => x.AttemptedAtUtc >= normalizedFromUtc && x.AttemptedAtUtc < normalizedToUtc)
            .Select(x => new NotificationQueueRow(x.Status, x.AttemptedAtUtc))
            .ToListAsync(ct);

        var openIncidents = await _db.CheckoutAlertIncidents.AsNoTracking()
            .CountAsync(x => x.ResolvedAtUtc == null, ct);

        var failures = checkoutEvents.Count(IsCheckoutFailureEvent);
        var serverErrorProxy = checkoutEvents.Count(x => LooksLikeServerError(x.ErrorCode, x.ErrorMessage));

        var errorSummary = new ErrorSummaryDto(
            TotalCheckoutEvents: checkoutEvents.Count,
            FailureEvents: failures,
            FailureRatePercent: Percent(failures, checkoutEvents.Count),
            ServerErrorProxyCount: serverErrorProxy,
            ServerErrorProxyRatePercent: Percent(serverErrorProxy, checkoutEvents.Count),
            TopErrorCodes: checkoutEvents
                .Where(x => !string.IsNullOrWhiteSpace(x.ErrorCode))
                .GroupBy(x => x.ErrorCode!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(5)
                .Select(g => new KeyCountDto(g.Key, g.Count()))
                .ToList());

        var jobs = new JobSummaryDto(
            CartRecovery: BuildCartJobStats(cartRuns),
            Reconciliation: BuildReconciliationJobStats(reconciliationRuns),
            MarketingAutomation: BuildMarketingJobStats(marketingRuns));

        var pendingAbandoned = abandonedRows.Count(x => IsStatus(x.ReminderStatus, "Pending"));
        var failedAbandoned = abandonedRows.Count(x => IsStatus(x.ReminderStatus, "Failed") || IsStatus(x.ReminderStatus, "Partial"));
        var sentAbandoned = abandonedRows.Count(x => IsStatus(x.ReminderStatus, "Sent"));
        var skippedAbandoned = abandonedRows.Count(x => IsStatus(x.ReminderStatus, "Skipped"));

        var failedNotifications = notificationRows.Count(x => IsStatus(x.Status, "Failed"));
        var sentNotifications = notificationRows.Count(x => IsStatus(x.Status, "Sent"));

        var oldestPendingAbandoned = abandonedRows
            .Where(x => IsStatus(x.ReminderStatus, "Pending"))
            .Select(x => (DateTime?)x.DetectedAtUtc)
            .OrderBy(x => x)
            .FirstOrDefault();

        var queue = new QueueSummaryDto(
            PendingAbandonedCarts: pendingAbandoned,
            FailedAbandonedCarts: failedAbandoned,
            SentAbandonedCarts: sentAbandoned,
            SkippedAbandonedCarts: skippedAbandoned,
            FailedNotifications: failedNotifications,
            SentNotifications: sentNotifications,
            OpenCheckoutIncidents: openIncidents,
            QueueBacklogApprox: pendingAbandoned + failedNotifications,
            OldestPendingAbandonedAtUtc: oldestPendingAbandoned,
            OldestPendingAbandonedAgeMinutes: oldestPendingAbandoned.HasValue
                ? (int)Math.Max(0, Math.Round((DateTime.UtcNow - oldestPendingAbandoned.Value).TotalMinutes, MidpointRounding.AwayFromZero))
                : null);

        return new ProductionDashboardDto(
            normalizedFromUtc,
            normalizedToUtc,
            DateTime.UtcNow,
            snapshot,
            errorSummary,
            jobs,
            queue);
    }

    private static JobPipelineDto BuildCartJobStats(IReadOnlyList<CartJobRunRow> runs)
    {
        var durations = runs.Select(x => x.DurationMs).Where(x => x >= 0).OrderBy(x => x).ToList();
        return new JobPipelineDto(
            "CartRecovery",
            runs.Count,
            runs.Count(x => x.Success),
            runs.Count(x => !x.Success),
            0,
            Percent(runs.Count(x => x.Success), runs.Count),
            Percentile(durations, 50),
            Percentile(durations, 95),
            runs.OrderByDescending(x => x.EndedAtUtc).Select(x => (DateTime?)x.EndedAtUtc).FirstOrDefault(),
            runs.OrderByDescending(x => x.EndedAtUtc).Select(x => x.Error).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static JobPipelineDto BuildReconciliationJobStats(IReadOnlyList<ReconciliationJobRunRow> runs)
    {
        var durations = runs.Select(x => x.DurationMs ?? -1).Where(x => x >= 0).OrderBy(x => x).ToList();
        var completed = runs.Where(x => !x.InProgress).ToList();
        return new JobPipelineDto(
            "Reconciliation",
            runs.Count,
            completed.Count(x => x.Success),
            completed.Count(x => !x.Success),
            runs.Count(x => x.InProgress),
            Percent(completed.Count(x => x.Success), completed.Count),
            Percentile(durations, 50),
            Percentile(durations, 95),
            runs.OrderByDescending(x => x.EndedAtUtc ?? x.StartedAtUtc).Select(x => x.EndedAtUtc ?? x.StartedAtUtc).FirstOrDefault(),
            runs.OrderByDescending(x => x.EndedAtUtc ?? x.StartedAtUtc).Select(x => x.Error).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static JobPipelineDto BuildMarketingJobStats(IReadOnlyList<MarketingJobRunRow> runs)
    {
        var completedRuns = runs.Where(x => x.CompletedAtUtc.HasValue).ToList();
        var durations = completedRuns
            .Select(x => (int)Math.Max(0, Math.Round((x.CompletedAtUtc!.Value - x.StartedAtUtc).TotalMilliseconds, MidpointRounding.AwayFromZero)))
            .OrderBy(x => x)
            .ToList();

        return new JobPipelineDto(
            "MarketingAutomation",
            runs.Count,
            runs.Count(x => IsStatus(x.Status, "Completed")),
            runs.Count(x => IsStatus(x.Status, "Failed") || IsStatus(x.Status, "Partial")),
            runs.Count(x => IsStatus(x.Status, "Running")),
            Percent(runs.Count(x => IsStatus(x.Status, "Completed")), runs.Count),
            Percentile(durations, 50),
            Percentile(durations, 95),
            runs.OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc).Select(x => x.CompletedAtUtc ?? x.StartedAtUtc).FirstOrDefault(),
            runs.OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc).Select(x => x.Notes).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static bool IsCheckoutFailureEvent(CheckoutEventErrorRow row)
        => row.Success == false ||
           IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentFailed) ||
           IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentInitiationFailed) ||
           IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentRedirectFailed);

    private static bool LooksLikeServerError(string? errorCode, string? errorMessage)
    {
        static bool ContainsServerCode(string value)
            => value.Contains("500", StringComparison.OrdinalIgnoreCase)
               || value.Contains("502", StringComparison.OrdinalIgnoreCase)
               || value.Contains("503", StringComparison.OrdinalIgnoreCase)
               || value.Contains("504", StringComparison.OrdinalIgnoreCase)
               || value.Contains("http_5", StringComparison.OrdinalIgnoreCase)
               || value.Contains("5xx", StringComparison.OrdinalIgnoreCase)
               || value.Contains("server_error", StringComparison.OrdinalIgnoreCase)
               || value.Contains("upstream", StringComparison.OrdinalIgnoreCase);

        return (!string.IsNullOrWhiteSpace(errorCode) && ContainsServerCode(errorCode))
               || (!string.IsNullOrWhiteSpace(errorMessage) && ContainsServerCode(errorMessage));
    }

    private static bool IsEvent(string? eventType, string expected)
        => string.Equals(eventType, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsStatus(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static decimal Percent(int numerator, int denominator)
        => denominator <= 0
            ? 0m
            : decimal.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    private static int? Percentile(IReadOnlyList<int> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return null;
        }

        percentile = Math.Clamp(percentile, 0, 100);
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var rank = (percentile / 100d) * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var lower = sortedValues[lowerIndex];
        var upper = sortedValues[upperIndex];
        var weight = rank - lowerIndex;
        return (int)Math.Round(lower + ((upper - lower) * weight), MidpointRounding.AwayFromZero);
    }

    private sealed record CheckoutEventErrorRow(
        string EventType,
        bool? Success,
        string? ErrorCode,
        string? ErrorMessage,
        DateTime OccurredAtUtc);

    private sealed record CartJobRunRow(
        DateTime StartedAtUtc,
        DateTime EndedAtUtc,
        int DurationMs,
        bool Success,
        string? Error);

    private sealed record ReconciliationJobRunRow(
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        int? DurationMs,
        bool Success,
        bool InProgress,
        string? Error);

    private sealed record MarketingJobRunRow(
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        string Status,
        int FailedCount,
        string? Notes);

    private sealed record AbandonedQueueRow(string ReminderStatus, DateTime DetectedAtUtc);

    private sealed record NotificationQueueRow(string Status, DateTime AttemptedAtUtc);

    public sealed record ProductionDashboardDto(
        DateTime FromUtc,
        DateTime ToUtc,
        DateTime GeneratedAtUtc,
        CheckoutObservabilityCalculator.CheckoutSnapshotDto Checkout,
        ErrorSummaryDto Errors,
        JobSummaryDto Jobs,
        QueueSummaryDto Queue);

    public sealed record ErrorSummaryDto(
        int TotalCheckoutEvents,
        int FailureEvents,
        decimal FailureRatePercent,
        int ServerErrorProxyCount,
        decimal ServerErrorProxyRatePercent,
        IReadOnlyList<KeyCountDto> TopErrorCodes);

    public sealed record KeyCountDto(string Key, int Count);

    public sealed record JobSummaryDto(
        JobPipelineDto CartRecovery,
        JobPipelineDto Reconciliation,
        JobPipelineDto MarketingAutomation);

    public sealed record JobPipelineDto(
        string JobName,
        int Runs,
        int SuccessRuns,
        int FailedRuns,
        int InProgressRuns,
        decimal SuccessRatePercent,
        int? DurationP50Ms,
        int? DurationP95Ms,
        DateTime? LastRunAtUtc,
        string? LastError);

    public sealed record QueueSummaryDto(
        int PendingAbandonedCarts,
        int FailedAbandonedCarts,
        int SentAbandonedCarts,
        int SkippedAbandonedCarts,
        int FailedNotifications,
        int SentNotifications,
        int OpenCheckoutIncidents,
        int QueueBacklogApprox,
        DateTime? OldestPendingAbandonedAtUtc,
        int? OldestPendingAbandonedAgeMinutes);
}
