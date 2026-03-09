using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public sealed class CheckoutObservabilityCalculator
{
    private readonly DoorMarketDbContext _db;

    public CheckoutObservabilityCalculator(DoorMarketDbContext db)
    {
        _db = db;
    }

    public async Task<CheckoutSnapshotDto> GetSnapshotAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default,
        AlertThresholds? thresholds = null)
    {
        var activeThresholds = thresholds ?? AlertThresholds.Default;
        var events = await QueryEventsWindow(fromUtc, toUtc)
            .Select(x => new CheckoutEventRow(
                x.EventType,
                x.PaymentProvider,
                x.ExperimentName,
                x.ExperimentGroup,
                x.Success,
                x.DurationMs,
                x.OccurredAtUtc))
            .ToListAsync(ct);

        var checkoutViews = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.CheckoutViewed));
        var submitClicks = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.CheckoutSubmitClicked));
        var ordersCreated = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.OrderCreated));
        var paymentsInitiated = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentInitiated));
        var paymentsConfirmed = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentConfirmed));
        var paymentsFailed = events.Count(IsPaymentFailureEvent);
        var redirectsFailed = events.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentRedirectFailed));

        var submitToOrderRate = Percent(ordersCreated, submitClicks);
        var orderToPaidRate = Percent(paymentsConfirmed, ordersCreated);
        var paymentSuccessRate = Percent(paymentsConfirmed, paymentsInitiated);
        var dropBeforePaymentRate = Percent(Math.Max(0, submitClicks - paymentsInitiated), submitClicks);

        var durations = events
            .Where(x => x.DurationMs.HasValue)
            .Select(x => x.DurationMs!.Value)
            .OrderBy(x => x)
            .ToList();

        var providerRows = events
            .Where(x => !string.IsNullOrWhiteSpace(x.PaymentProvider))
            .GroupBy(x => x.PaymentProvider!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var initiated = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentInitiated));
                var confirmed = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentConfirmed));
                var failed = g.Count(IsPaymentFailureEvent);
                var providerDurations = g.Where(x => x.DurationMs.HasValue)
                    .Select(x => x.DurationMs!.Value)
                    .OrderBy(x => x)
                    .ToList();

                return new ProviderQualityRowDto(
                    g.Key,
                    initiated,
                    confirmed,
                    failed,
                    Percent(confirmed, initiated),
                    Percent(failed, initiated),
                    Percentile(providerDurations, 50),
                    Percentile(providerDurations, 95));
            })
            .ToList();

        var alerts = BuildAlerts(
            fromUtc,
            toUtc,
            submitClicks,
            ordersCreated,
            paymentsInitiated,
            paymentsConfirmed,
            redirectsFailed,
            durations,
            providerRows,
            activeThresholds);

        return new CheckoutSnapshotDto(
            fromUtc,
            toUtc,
            checkoutViews,
            submitClicks,
            ordersCreated,
            paymentsInitiated,
            paymentsConfirmed,
            paymentsFailed,
            redirectsFailed,
            submitToOrderRate,
            orderToPaidRate,
            paymentSuccessRate,
            dropBeforePaymentRate,
            Percentile(durations, 50),
            Percentile(durations, 95),
            providerRows,
            alerts.Count);
    }

    public async Task<IReadOnlyList<RealtimePointDto>> GetRealtimeSeriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int bucketMinutes,
        CancellationToken ct = default)
    {
        bucketMinutes = Math.Clamp(bucketMinutes, 1, 60);
        var bucket = TimeSpan.FromMinutes(bucketMinutes);

        var events = await _db.CheckoutAnalyticsEvents.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .Where(x =>
                x.EventType == CheckoutObservabilityEvents.CheckoutSubmitClicked ||
                x.EventType == CheckoutObservabilityEvents.OrderCreated ||
                x.EventType == CheckoutObservabilityEvents.PaymentInitiated ||
                x.EventType == CheckoutObservabilityEvents.PaymentConfirmed ||
                x.EventType == CheckoutObservabilityEvents.PaymentFailed ||
                x.EventType == CheckoutObservabilityEvents.PaymentInitiationFailed ||
                x.EventType == CheckoutObservabilityEvents.PaymentRedirectFailed)
            .Select(x => new CheckoutEventRow(
                x.EventType,
                x.PaymentProvider,
                x.ExperimentName,
                x.ExperimentGroup,
                x.Success,
                x.DurationMs,
                x.OccurredAtUtc))
            .ToListAsync(ct);

        var bucketTicks = bucket.Ticks;
        var grouped = events
            .GroupBy(x => FloorUtc(x.OccurredAtUtc, bucketTicks))
            .ToDictionary(g => g.Key, g => g.ToList());

        var alignedFrom = FloorUtc(fromUtc, bucketTicks);
        var result = new List<RealtimePointDto>();
        for (var cursor = alignedFrom; cursor < toUtc; cursor = cursor.Add(bucket))
        {
            grouped.TryGetValue(cursor, out var rows);
            rows ??= new List<CheckoutEventRow>();

            var submit = rows.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.CheckoutSubmitClicked));
            var orders = rows.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.OrderCreated));
            var initiated = rows.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentInitiated));
            var confirmed = rows.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentConfirmed));
            var failed = rows.Count(IsPaymentFailureEvent);

            result.Add(new RealtimePointDto(
                cursor,
                cursor.Add(bucket),
                submit,
                orders,
                initiated,
                confirmed,
                failed,
                Percent(confirmed, initiated),
                Percent(orders, submit)));
        }

        return result;
    }

    public async Task<IReadOnlyList<PaymentQualityAlertDto>> GetPaymentQualityAlertsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default,
        AlertThresholds? thresholds = null)
    {
        var activeThresholds = thresholds ?? AlertThresholds.Default;
        var snapshot = await GetSnapshotAsync(fromUtc, toUtc, ct, activeThresholds);
        var events = await QueryEventsWindow(fromUtc, toUtc)
            .Select(x => new CheckoutEventRow(
                x.EventType,
                x.PaymentProvider,
                x.ExperimentName,
                x.ExperimentGroup,
                x.Success,
                x.DurationMs,
                x.OccurredAtUtc))
            .ToListAsync(ct);
        return BuildAlerts(
            fromUtc,
            toUtc,
            snapshot.SubmitClicks,
            snapshot.OrdersCreated,
            snapshot.PaymentsInitiated,
            snapshot.PaymentsConfirmed,
            snapshot.RedirectsFailed,
            events.Where(x => x.DurationMs.HasValue).Select(x => x.DurationMs!.Value).OrderBy(x => x).ToList(),
            snapshot.Providers,
            activeThresholds);
    }

    public async Task<IReadOnlyList<AbUxRowDto>> GetAbUxRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var events = await QueryEventsWindow(fromUtc, toUtc)
            .Where(x => x.ExperimentGroup != null && x.ExperimentGroup != string.Empty)
            .Select(x => new CheckoutEventRow(
                x.EventType,
                x.PaymentProvider,
                x.ExperimentName,
                x.ExperimentGroup,
                x.Success,
                x.DurationMs,
                x.OccurredAtUtc))
            .ToListAsync(ct);

        var rows = events
            .GroupBy(x => new
            {
                ExperimentName = string.IsNullOrWhiteSpace(x.ExperimentName)
                    ? CheckoutObservabilityEvents.ExperimentCheckoutUxV1
                    : x.ExperimentName!,
                ExperimentGroup = x.ExperimentGroup!
            })
            .OrderBy(g => g.Key.ExperimentName)
            .ThenBy(g => g.Key.ExperimentGroup)
            .Select(g =>
            {
                var viewed = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.CheckoutViewed));
                var submit = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.CheckoutSubmitClicked));
                var orderCreated = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.OrderCreated));
                var paid = g.Count(x => IsEvent(x.EventType, CheckoutObservabilityEvents.PaymentConfirmed));
                return new AbUxRowDto(
                    g.Key.ExperimentName,
                    g.Key.ExperimentGroup,
                    viewed,
                    submit,
                    orderCreated,
                    paid,
                    Percent(submit, viewed),
                    Percent(orderCreated, submit),
                    Percent(paid, orderCreated),
                    Percent(paid, viewed));
            })
            .ToList();

        var groupedByExperiment = rows
            .GroupBy(x => x.ExperimentName)
            .SelectMany(group =>
            {
                var control = group.FirstOrDefault(x => string.Equals(x.ExperimentGroup, "A", StringComparison.OrdinalIgnoreCase))
                    ?? group.FirstOrDefault();
                if (control is null)
                {
                    return group;
                }

                return group.Select(x => x with
                {
                    UpliftVsControlPct = decimal.Round(x.OverallConversionRate - control.OverallConversionRate, 2, MidpointRounding.AwayFromZero)
                });
            })
            .OrderBy(x => x.ExperimentName)
            .ThenBy(x => x.ExperimentGroup)
            .ToList();

        return groupedByExperiment;
    }

    private IQueryable<DoorMarket.Domain.Entities.CheckoutAnalyticsEvent> QueryEventsWindow(DateTime fromUtc, DateTime toUtc)
    {
        return _db.CheckoutAnalyticsEvents.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc);
    }

    private static List<PaymentQualityAlertDto> BuildAlerts(
        DateTime fromUtc,
        DateTime toUtc,
        int submitClicks,
        int ordersCreated,
        int paymentsInitiated,
        int paymentsConfirmed,
        int redirectsFailed,
        IReadOnlyList<int> durations,
        IReadOnlyList<ProviderQualityRowDto> providers,
        AlertThresholds thresholds)
    {
        var alerts = new List<PaymentQualityAlertDto>();

        var paymentSuccessRate = Percent(paymentsConfirmed, paymentsInitiated);
        if (paymentsInitiated >= thresholds.PaymentSuccessCriticalMinInitiatedCount &&
            paymentSuccessRate < thresholds.PaymentSuccessCriticalThresholdPct)
        {
            alerts.Add(new PaymentQualityAlertDto(
                "critical_payment_success_drop",
                "Critical",
                "Payment success rate critically low",
                $"Confirmations dropped below {thresholds.PaymentSuccessCriticalThresholdPct:0.##}% for the selected period.",
                null,
                paymentSuccessRate,
                thresholds.PaymentSuccessCriticalThresholdPct,
                fromUtc,
                toUtc));
        }
        else if (paymentsInitiated >= thresholds.PaymentSuccessWarningMinInitiatedCount &&
                 paymentSuccessRate < thresholds.PaymentSuccessWarningThresholdPct)
        {
            alerts.Add(new PaymentQualityAlertDto(
                "warning_payment_success_drop",
                "Warning",
                "Payment success rate degraded",
                $"Confirmations dropped below {thresholds.PaymentSuccessWarningThresholdPct:0.##}% for the selected period.",
                null,
                paymentSuccessRate,
                thresholds.PaymentSuccessWarningThresholdPct,
                fromUtc,
                toUtc));
        }

        var submitToOrderRate = Percent(ordersCreated, submitClicks);
        if (submitClicks >= thresholds.SubmitToOrderWarningMinSubmitCount &&
            submitToOrderRate < thresholds.SubmitToOrderWarningThresholdPct)
        {
            alerts.Add(new PaymentQualityAlertDto(
                "warning_checkout_drop_submit_to_order",
                "Warning",
                "Checkout drop before order creation",
                "Submit clicks are not converting enough into orders.",
                null,
                submitToOrderRate,
                thresholds.SubmitToOrderWarningThresholdPct,
                fromUtc,
                toUtc));
        }

        var p95 = Percentile(durations, 95);
        if (p95.HasValue && p95.Value > thresholds.PaymentLatencyP95WarningMs)
        {
            alerts.Add(new PaymentQualityAlertDto(
                "warning_payment_latency_p95",
                "Warning",
                "Payment latency high",
                $"Payment flow p95 latency exceeded {thresholds.PaymentLatencyP95WarningMs}ms.",
                null,
                p95.Value,
                thresholds.PaymentLatencyP95WarningMs,
                fromUtc,
                toUtc));
        }

        if (redirectsFailed >= thresholds.RedirectFailuresWarningCount)
        {
            alerts.Add(new PaymentQualityAlertDto(
                "warning_redirect_failures",
                "Warning",
                "Checkout redirect failures detected",
                "External payment redirects are failing too often.",
                null,
                redirectsFailed,
                thresholds.RedirectFailuresWarningCount,
                fromUtc,
                toUtc));
        }

        foreach (var provider in providers)
        {
            if (provider.InitiatedCount < thresholds.ProviderFailureMinInitiatedCount)
            {
                continue;
            }

            if (provider.FailureRatePercent >= thresholds.ProviderFailureCriticalThresholdPct)
            {
                alerts.Add(new PaymentQualityAlertDto(
                    $"critical_provider_failure_{provider.Provider.ToLowerInvariant()}",
                    "Critical",
                    $"Provider {provider.Provider} failure spike",
                    $"Provider failure rate exceeded {thresholds.ProviderFailureCriticalThresholdPct:0.##}%.",
                    provider.Provider,
                    provider.FailureRatePercent,
                    thresholds.ProviderFailureCriticalThresholdPct,
                    fromUtc,
                    toUtc));
                continue;
            }

            if (provider.FailureRatePercent >= thresholds.ProviderFailureWarningThresholdPct)
            {
                alerts.Add(new PaymentQualityAlertDto(
                    $"warning_provider_failure_{provider.Provider.ToLowerInvariant()}",
                    "Warning",
                    $"Provider {provider.Provider} degradation",
                    $"Provider failure rate exceeded {thresholds.ProviderFailureWarningThresholdPct:0.##}%.",
                    provider.Provider,
                    provider.FailureRatePercent,
                    thresholds.ProviderFailureWarningThresholdPct,
                    fromUtc,
                    toUtc));
            }
        }

        return alerts
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Code)
            .ToList();
    }

    private static bool IsPaymentFailureEvent(CheckoutEventRow row)
        => IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentFailed) ||
           IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentInitiationFailed) ||
           IsEvent(row.EventType, CheckoutObservabilityEvents.PaymentRedirectFailed) ||
           row.Success == false;

    private static bool IsEvent(string? eventType, string expected)
        => string.Equals(eventType, expected, StringComparison.OrdinalIgnoreCase);

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

    private static DateTime FloorUtc(DateTime value, long bucketTicks)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime((utc.Ticks / bucketTicks) * bucketTicks, DateTimeKind.Utc);
    }

    private sealed record CheckoutEventRow(
        string EventType,
        string? PaymentProvider,
        string? ExperimentName,
        string? ExperimentGroup,
        bool? Success,
        int? DurationMs,
        DateTime OccurredAtUtc);

    public sealed record CheckoutSnapshotDto(
        DateTime FromUtc,
        DateTime ToUtc,
        int CheckoutViews,
        int SubmitClicks,
        int OrdersCreated,
        int PaymentsInitiated,
        int PaymentsConfirmed,
        int PaymentsFailed,
        int RedirectsFailed,
        decimal SubmitToOrderRate,
        decimal OrderToPaidRate,
        decimal PaymentSuccessRate,
        decimal DropBeforePaymentRate,
        int? LatencyP50Ms,
        int? LatencyP95Ms,
        IReadOnlyList<ProviderQualityRowDto> Providers,
        int AlertsCount);

    public sealed record ProviderQualityRowDto(
        string Provider,
        int InitiatedCount,
        int ConfirmedCount,
        int FailedCount,
        decimal SuccessRatePercent,
        decimal FailureRatePercent,
        int? LatencyP50Ms,
        int? LatencyP95Ms);

    public sealed record RealtimePointDto(
        DateTime BucketStartUtc,
        DateTime BucketEndUtc,
        int SubmitClicks,
        int OrdersCreated,
        int PaymentsInitiated,
        int PaymentsConfirmed,
        int PaymentsFailed,
        decimal PaymentSuccessRate,
        decimal SubmitToOrderRate);

    public sealed record PaymentQualityAlertDto(
        string Code,
        string Severity,
        string Title,
        string Description,
        string? Provider,
        decimal ObservedValue,
        decimal ThresholdValue,
        DateTime FromUtc,
        DateTime ToUtc);

    public sealed record AbUxRowDto(
        string ExperimentName,
        string ExperimentGroup,
        int CheckoutViews,
        int SubmitClicks,
        int OrdersCreated,
        int PaymentsConfirmed,
        decimal ViewToSubmitRate,
        decimal SubmitToOrderRate,
        decimal OrderToPaidRate,
        decimal OverallConversionRate,
        decimal UpliftVsControlPct = 0m);

    public sealed record AlertThresholds(
        decimal PaymentSuccessCriticalThresholdPct,
        decimal PaymentSuccessWarningThresholdPct,
        int PaymentSuccessCriticalMinInitiatedCount,
        int PaymentSuccessWarningMinInitiatedCount,
        decimal SubmitToOrderWarningThresholdPct,
        int SubmitToOrderWarningMinSubmitCount,
        int PaymentLatencyP95WarningMs,
        int RedirectFailuresWarningCount,
        decimal ProviderFailureCriticalThresholdPct,
        decimal ProviderFailureWarningThresholdPct,
        int ProviderFailureMinInitiatedCount)
    {
        public static AlertThresholds Default { get; } = new(
            PaymentSuccessCriticalThresholdPct: 65m,
            PaymentSuccessWarningThresholdPct: 80m,
            PaymentSuccessCriticalMinInitiatedCount: 20,
            PaymentSuccessWarningMinInitiatedCount: 10,
            SubmitToOrderWarningThresholdPct: 60m,
            SubmitToOrderWarningMinSubmitCount: 20,
            PaymentLatencyP95WarningMs: 12_000,
            RedirectFailuresWarningCount: 5,
            ProviderFailureCriticalThresholdPct: 40m,
            ProviderFailureWarningThresholdPct: 25m,
            ProviderFailureMinInitiatedCount: 8);
    }
}
