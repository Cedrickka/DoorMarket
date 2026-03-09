using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Services;

public interface IReconciliationCronService
{
    Task<ReconciliationCronRunResultDto> RunOnceAsync(string triggerSource, Guid? triggeredByUserId, CancellationToken ct = default);
    Task<ReconciliationCronRunsPageDto> GetRunsAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default);
    Task<ReconciliationCronSummaryDto> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

public sealed class ReconciliationCronService : IReconciliationCronService
{
    private const string PayoutStatusDraft = "Draft";
    private const string PayoutStatusApproved = "Approved";
    private const string PayoutStatusPaid = "Paid";
    private const string PayoutStatusReversed = "Reversed";
    private const string TriggerSourceWorker = "Worker";
    private const string TriggerSourceManual = "Manual";

    private readonly DoorMarketDbContext _db;
    private readonly ReconciliationCronOptions _options;
    private readonly ILogger<ReconciliationCronService> _logger;

    public ReconciliationCronService(
        DoorMarketDbContext db,
        IOptions<ReconciliationCronOptions> options,
        ILogger<ReconciliationCronService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReconciliationCronRunResultDto> RunOnceAsync(string triggerSource, Guid? triggeredByUserId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new ReconciliationCronRunResultDto(
                RunId: null,
                AcquiredLock: false,
                Success: false,
                WasSkipped: true,
                SkipReason: "Reconciliation cron disabled by configuration.",
                TriggerSource: NormalizeTriggerSource(triggerSource),
                TriggeredByUserId: triggeredByUserId,
                StartedAtUtc: DateTime.UtcNow,
                EndedAtUtc: DateTime.UtcNow,
                DurationMs: 0,
                WindowFromUtc: DateTime.UtcNow,
                WindowToUtc: DateTime.UtcNow,
                DraftSlaDays: 0,
                ApprovedSlaDays: 0,
                CandidatePayouts: 0,
                StaleDraftCount: 0,
                StaleApprovedCount: 0,
                PartialPaidCount: 0,
                PaidWithoutReferenceCount: 0,
                OverlapPairCount: 0,
                ReversedCount: 0,
                ReversalRatePercent: 0m,
                PartialGapTotal: 0m,
                InsightsCount: 0,
                Error: null);
        }

        var now = DateTime.UtcNow;
        var trigger = NormalizeTriggerSource(triggerSource);
        var draftSlaDays = Math.Clamp(_options.DraftSlaDays, 1, 90);
        var approvedSlaDays = Math.Clamp(_options.ApprovedSlaDays, 1, 90);
        var windowDays = Math.Clamp(_options.WindowDays, 1, 365);
        var lockStaleAfterMinutes = Math.Clamp(_options.LockStaleAfterMinutes, 5, 24 * 24 * 60);
        var windowFromUtc = now.AddDays(-windowDays);
        var windowToUtc = now;

        await ReleaseStaleInProgressRunsAsync(now, lockStaleAfterMinutes, ct);

        var run = new ReconciliationJobRun
        {
            TriggerSource = trigger,
            TriggeredByUserId = triggeredByUserId,
            StartedAtUtc = now,
            InProgress = true,
            Success = false,
            WasSkipped = false,
            WindowFromUtc = windowFromUtc,
            WindowToUtc = windowToUtc,
            DraftSlaDays = draftSlaDays,
            ApprovedSlaDays = approvedSlaDays
        };

        _db.ReconciliationJobRuns.Add(run);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsInProgressUniqueViolation(ex))
        {
            _db.ChangeTracker.Clear();
            var activeRun = await _db.ReconciliationJobRuns.AsNoTracking()
                .Where(x => x.InProgress)
                .OrderByDescending(x => x.StartedAtUtc)
                .Select(x => new { x.Id, x.StartedAtUtc, x.TriggerSource })
                .FirstOrDefaultAsync(ct);

            var reason = activeRun is null
                ? "Another reconciliation run is currently in progress."
                : $"Run {activeRun.Id} started at {activeRun.StartedAtUtc:O} ({activeRun.TriggerSource}) is still in progress.";

            var skipRow = new ReconciliationJobRun
            {
                TriggerSource = trigger,
                TriggeredByUserId = triggeredByUserId,
                StartedAtUtc = now,
                EndedAtUtc = now,
                DurationMs = 0,
                Success = false,
                InProgress = false,
                WasSkipped = true,
                SkipReason = Truncate(reason, 320),
                WindowFromUtc = windowFromUtc,
                WindowToUtc = windowToUtc,
                DraftSlaDays = draftSlaDays,
                ApprovedSlaDays = approvedSlaDays
            };
            _db.ReconciliationJobRuns.Add(skipRow);
            await _db.SaveChangesAsync(ct);

            return ToResult(skipRow, acquiredLock: false);
        }

        try
        {
            var payouts = await _db.ShopPayouts.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= windowFromUtc && x.CreatedAtUtc < windowToUtc)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Clamp(_options.MaxPayoutsScan, 200, 200_000))
                .Select(x => new PayoutProjection(
                    x.Id,
                    x.ShopId,
                    x.Currency,
                    x.Status,
                    x.AmountPaid,
                    x.NetToPay,
                    x.Reference,
                    x.CreatedAtUtc,
                    x.ApprovedAtUtc,
                    x.PeriodStartUtc,
                    x.PeriodEndUtc))
                .ToListAsync(ct);

            var staleDrafts = payouts
                .Where(x => string.Equals(x.Status, PayoutStatusDraft, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.CreatedAtUtc <= now.AddDays(-draftSlaDays))
                .ToList();

            var staleApproved = payouts
                .Where(x => string.Equals(x.Status, PayoutStatusApproved, StringComparison.OrdinalIgnoreCase))
                .Where(x => (x.ApprovedAtUtc ?? x.CreatedAtUtc) <= now.AddDays(-approvedSlaDays))
                .ToList();

            var paidWithoutReference = payouts
                .Where(x => string.Equals(x.Status, PayoutStatusPaid, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(x.Reference))
                .ToList();

            var partialPaid = payouts
                .Where(x => string.Equals(x.Status, PayoutStatusPaid, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.AmountPaid < x.NetToPay)
                .ToList();

            var overlapPairs = payouts
                .Where(x => !string.Equals(x.Status, PayoutStatusReversed, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => new { x.ShopId, x.Currency })
                .SelectMany(g =>
                {
                    var ordered = g.OrderBy(x => x.PeriodStartUtc).ThenBy(x => x.PeriodEndUtc).ToList();
                    var overlaps = new List<(Guid First, Guid Second)>();
                    for (var i = 1; i < ordered.Count; i++)
                    {
                        var previous = ordered[i - 1];
                        var current = ordered[i];
                        if (current.PeriodStartUtc < previous.PeriodEndUtc)
                        {
                            overlaps.Add((previous.Id, current.Id));
                        }
                    }

                    return overlaps;
                })
                .ToList();

            var reversedCount = payouts.Count(x => string.Equals(x.Status, PayoutStatusReversed, StringComparison.OrdinalIgnoreCase));
            var closedCount = payouts.Count(x =>
                string.Equals(x.Status, PayoutStatusPaid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, PayoutStatusReversed, StringComparison.OrdinalIgnoreCase));
            var reversalRate = closedCount > 0
                ? decimal.Round((decimal)reversedCount * 100m / closedCount, 2, MidpointRounding.AwayFromZero)
                : 0m;
            var partialGapTotal = decimal.Round(partialPaid.Sum(x => x.NetToPay - x.AmountPaid), 2, MidpointRounding.AwayFromZero);

            var insightsCount = 0;
            if (staleDrafts.Count > 0)
            {
                insightsCount++;
            }

            if (staleApproved.Count > 0)
            {
                insightsCount++;
            }

            if (partialPaid.Count > 0)
            {
                insightsCount++;
            }

            if (paidWithoutReference.Count > 0)
            {
                insightsCount++;
            }

            if (overlapPairs.Count > 0)
            {
                insightsCount++;
            }

            if (reversalRate >= 15m && closedCount >= 5)
            {
                insightsCount++;
            }

            var ended = DateTime.UtcNow;
            run.InProgress = false;
            run.Success = true;
            run.WasSkipped = false;
            run.EndedAtUtc = ended;
            run.DurationMs = (int)Math.Max(0, (ended - run.StartedAtUtc).TotalMilliseconds);
            run.CandidatePayouts = payouts.Count;
            run.StaleDraftCount = staleDrafts.Count;
            run.StaleApprovedCount = staleApproved.Count;
            run.PartialPaidCount = partialPaid.Count;
            run.PaidWithoutReferenceCount = paidWithoutReference.Count;
            run.OverlapPairCount = overlapPairs.Count;
            run.ReversedCount = reversedCount;
            run.InsightsCount = insightsCount;
            run.ReversalRatePercent = reversalRate;
            run.PartialGapTotal = partialGapTotal;
            run.UpdatedAtUtc = ended;
            await _db.SaveChangesAsync(ct);

            return ToResult(run, acquiredLock: true);
        }
        catch (Exception ex)
        {
            var ended = DateTime.UtcNow;
            run.InProgress = false;
            run.Success = false;
            run.WasSkipped = false;
            run.EndedAtUtc = ended;
            run.DurationMs = (int)Math.Max(0, (ended - run.StartedAtUtc).TotalMilliseconds);
            run.Error = Truncate(ex.Message, 1200);
            run.UpdatedAtUtc = ended;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // Preserve the original failure even if persistence of run state also fails.
            }

            _logger.LogError(ex, "Reconciliation cron run failed. runId={RunId}", run.Id);
            return ToResult(run, acquiredLock: true);
        }
    }

    public async Task<ReconciliationCronRunsPageDto> GetRunsAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.ReconciliationJobRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= range.FromUtc && x.StartedAtUtc < range.ToUtc);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(x => ToResult(x, acquiredLock: !x.WasSkipped)).ToList();
        return new ReconciliationCronRunsPageDto(page, pageSize, total, items);
    }

    public async Task<ReconciliationCronSummaryDto> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        var runs = await _db.ReconciliationJobRuns.AsNoTracking()
            .Where(x => x.StartedAtUtc >= range.FromUtc && x.StartedAtUtc < range.ToUtc)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(ct);

        var totalRuns = runs.Count;
        var successRuns = runs.Count(x => x.Success);
        var skippedRuns = runs.Count(x => x.WasSkipped);
        var failedRuns = runs.Count(x => !x.Success && !x.WasSkipped);
        var activeRuns = runs.Count(x => x.InProgress);

        var durations = runs
            .Where(x => !x.WasSkipped && x.DurationMs.HasValue && x.DurationMs.Value >= 0)
            .Select(x => x.DurationMs!.Value)
            .ToList();

        var p50 = Percentile(durations, 50);
        var p95 = Percentile(durations, 95);

        var lastRunAtUtc = runs.FirstOrDefault()?.StartedAtUtc;
        var lastSuccessAtUtc = runs.FirstOrDefault(x => x.Success)?.StartedAtUtc;
        var lastFailureAtUtc = runs.FirstOrDefault(x => !x.Success && !x.WasSkipped)?.StartedAtUtc;
        var lastSkippedAtUtc = runs.FirstOrDefault(x => x.WasSkipped)?.StartedAtUtc;
        var lastActiveStartedAtUtc = runs.FirstOrDefault(x => x.InProgress)?.StartedAtUtc;

        var totalInsights = runs.Sum(x => x.InsightsCount);
        var totalScanned = runs.Sum(x => x.CandidatePayouts);
        var avgScanned = totalRuns == 0 ? 0m : decimal.Round((decimal)totalScanned / totalRuns, 2, MidpointRounding.AwayFromZero);
        var avgInsights = totalRuns == 0 ? 0m : decimal.Round((decimal)totalInsights / totalRuns, 2, MidpointRounding.AwayFromZero);
        var totalOverlapPairs = runs.Sum(x => x.OverlapPairCount);
        var totalPartialGap = decimal.Round(runs.Sum(x => x.PartialGapTotal), 2, MidpointRounding.AwayFromZero);

        return new ReconciliationCronSummaryDto(
            range.FromUtc,
            range.ToUtc,
            totalRuns,
            successRuns,
            failedRuns,
            skippedRuns,
            activeRuns,
            p50,
            p95,
            lastRunAtUtc,
            lastSuccessAtUtc,
            lastFailureAtUtc,
            lastSkippedAtUtc,
            lastActiveStartedAtUtc,
            totalInsights,
            totalScanned,
            avgScanned,
            avgInsights,
            totalOverlapPairs,
            totalPartialGap);
    }

    private async Task ReleaseStaleInProgressRunsAsync(DateTime now, int lockStaleAfterMinutes, CancellationToken ct)
    {
        var staleBefore = now.AddMinutes(-lockStaleAfterMinutes);
        var staleRuns = await _db.ReconciliationJobRuns
            .Where(x => x.InProgress && x.StartedAtUtc < staleBefore)
            .ToListAsync(ct);

        if (staleRuns.Count == 0)
        {
            return;
        }

        foreach (var stale in staleRuns)
        {
            stale.InProgress = false;
            stale.Success = false;
            stale.WasSkipped = true;
            stale.SkipReason = Truncate($"Guardrail released stale lock older than {lockStaleAfterMinutes} minute(s).", 320);
            stale.Error = Truncate("Marked as stale by reconciliation guardrail.", 1200);
            stale.EndedAtUtc = now;
            stale.DurationMs = (int)Math.Max(0, (now - stale.StartedAtUtc).TotalMilliseconds);
            stale.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static ReconciliationCronRunResultDto ToResult(ReconciliationJobRun row, bool acquiredLock)
    {
        return new ReconciliationCronRunResultDto(
            RunId: row.Id,
            AcquiredLock: acquiredLock,
            Success: row.Success,
            WasSkipped: row.WasSkipped,
            SkipReason: row.SkipReason,
            TriggerSource: row.TriggerSource,
            TriggeredByUserId: row.TriggeredByUserId,
            StartedAtUtc: row.StartedAtUtc,
            EndedAtUtc: row.EndedAtUtc,
            DurationMs: row.DurationMs,
            WindowFromUtc: row.WindowFromUtc,
            WindowToUtc: row.WindowToUtc,
            DraftSlaDays: row.DraftSlaDays,
            ApprovedSlaDays: row.ApprovedSlaDays,
            CandidatePayouts: row.CandidatePayouts,
            StaleDraftCount: row.StaleDraftCount,
            StaleApprovedCount: row.StaleApprovedCount,
            PartialPaidCount: row.PartialPaidCount,
            PaidWithoutReferenceCount: row.PaidWithoutReferenceCount,
            OverlapPairCount: row.OverlapPairCount,
            ReversedCount: row.ReversedCount,
            ReversalRatePercent: row.ReversalRatePercent,
            PartialGapTotal: row.PartialGapTotal,
            InsightsCount: row.InsightsCount,
            Error: row.Error);
    }

    private static bool IsInProgressUniqueViolation(DbUpdateException ex)
    {
        var text = ex.InnerException?.Message ?? ex.Message;
        return text.Contains("IX_ReconciliationJobRuns_InProgressUnique", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ReconciliationJobRuns", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTriggerSource(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.Equals(normalized, TriggerSourceManual, StringComparison.OrdinalIgnoreCase))
        {
            return TriggerSourceManual;
        }

        return TriggerSourceWorker;
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

    private static string? Truncate(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private sealed record PayoutProjection(
        Guid Id,
        Guid ShopId,
        string Currency,
        string Status,
        decimal AmountPaid,
        decimal NetToPay,
        string? Reference,
        DateTime CreatedAtUtc,
        DateTime? ApprovedAtUtc,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc);
}

public sealed record ReconciliationCronRunResultDto(
    Guid? RunId,
    bool AcquiredLock,
    bool Success,
    bool WasSkipped,
    string? SkipReason,
    string TriggerSource,
    Guid? TriggeredByUserId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int? DurationMs,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int DraftSlaDays,
    int ApprovedSlaDays,
    int CandidatePayouts,
    int StaleDraftCount,
    int StaleApprovedCount,
    int PartialPaidCount,
    int PaidWithoutReferenceCount,
    int OverlapPairCount,
    int ReversedCount,
    decimal ReversalRatePercent,
    decimal PartialGapTotal,
    int InsightsCount,
    string? Error);

public sealed record ReconciliationCronRunsPageDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<ReconciliationCronRunResultDto> Items);

public sealed record ReconciliationCronSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalRuns,
    int SuccessRuns,
    int FailedRuns,
    int SkippedRuns,
    int ActiveRuns,
    int? RunDurationP50Ms,
    int? RunDurationP95Ms,
    DateTime? LastRunAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    DateTime? LastSkippedAtUtc,
    DateTime? LastActiveStartedAtUtc,
    int TotalInsights,
    int TotalCandidatePayouts,
    decimal AvgCandidatePayoutsPerRun,
    decimal AvgInsightsPerRun,
    int TotalOverlapPairCount,
    decimal TotalPartialGap);
