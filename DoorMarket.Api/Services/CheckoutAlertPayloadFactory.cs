using DoorMarket.Api.Models;
using DoorMarket.Domain.Entities;

namespace DoorMarket.Api.Services;

public static class CheckoutAlertPayloadFactory
{
    private const string SchemaVersion = "checkout_payment_incident_v1";
    private const string EventType = "checkout_payment_incident";

    public static CheckoutPaymentIncidentPayload FromAlert(
        CheckoutObservabilityCalculator.PaymentQualityAlertDto alert,
        string correlationId,
        DateTime generatedAtUtc)
    {
        var comparator = ResolveComparator(alert.Code);
        var metricUnit = ResolveMetricUnit(alert.Code);
        var breachDelta = ResolveBreachDelta(alert.ObservedValue, alert.ThresholdValue, comparator);
        return new CheckoutPaymentIncidentPayload(
            SchemaVersion: SchemaVersion,
            EventType: EventType,
            CorrelationId: NormalizeCorrelationId(correlationId),
            GeneratedAtUtc: generatedAtUtc,
            IncidentStatus: "Detected",
            IncidentId: null,
            AlertCode: Normalize(alert.Code, 120, "unknown_alert"),
            Severity: Normalize(alert.Severity, 24, "Warning"),
            Title: Normalize(alert.Title, 200, "Checkout payment alert"),
            Description: Normalize(alert.Description, 800, "Payment quality alert detected."),
            Provider: NormalizeNullable(alert.Provider, 40),
            MetricUnit: metricUnit,
            ThresholdComparator: comparator,
            ObservedValue: alert.ObservedValue,
            ThresholdValue: alert.ThresholdValue,
            BreachDelta: breachDelta,
            IsBreached: breachDelta > 0m,
            WindowFromUtc: alert.FromUtc,
            WindowToUtc: alert.ToUtc,
            FirstTriggeredAtUtc: null,
            LastTriggeredAtUtc: null,
            TriggerCount: null,
            NotificationCount: null,
            LastNotifiedAtUtc: null,
            IsAcknowledged: null,
            AcknowledgedAtUtc: null,
            AcknowledgedBy: null,
            ResolvedAtUtc: null);
    }

    public static CheckoutPaymentIncidentPayload FromIncident(
        CheckoutAlertIncident incident,
        string correlationId,
        DateTime generatedAtUtc)
    {
        var comparator = ResolveComparator(incident.AlertCode);
        var metricUnit = ResolveMetricUnit(incident.AlertCode);
        var breachDelta = ResolveBreachDelta(incident.ObservedValue, incident.ThresholdValue, comparator);
        return new CheckoutPaymentIncidentPayload(
            SchemaVersion: SchemaVersion,
            EventType: EventType,
            CorrelationId: NormalizeCorrelationId(correlationId),
            GeneratedAtUtc: generatedAtUtc,
            IncidentStatus: ResolveIncidentStatus(incident),
            IncidentId: incident.Id,
            AlertCode: Normalize(incident.AlertCode, 120, "unknown_alert"),
            Severity: Normalize(incident.Severity, 24, "Warning"),
            Title: Normalize(incident.Title, 200, "Checkout payment alert"),
            Description: Normalize(incident.Description, 800, "Payment quality alert detected."),
            Provider: NormalizeNullable(incident.Provider, 40),
            MetricUnit: metricUnit,
            ThresholdComparator: comparator,
            ObservedValue: incident.ObservedValue,
            ThresholdValue: incident.ThresholdValue,
            BreachDelta: breachDelta,
            IsBreached: breachDelta > 0m,
            WindowFromUtc: incident.WindowFromUtc,
            WindowToUtc: incident.WindowToUtc,
            FirstTriggeredAtUtc: incident.FirstTriggeredAtUtc,
            LastTriggeredAtUtc: incident.LastTriggeredAtUtc,
            TriggerCount: incident.TriggerCount,
            NotificationCount: incident.NotificationCount,
            LastNotifiedAtUtc: incident.LastNotifiedAtUtc,
            IsAcknowledged: incident.IsAcknowledged,
            AcknowledgedAtUtc: incident.AcknowledgedAtUtc,
            AcknowledgedBy: NormalizeNullable(incident.AcknowledgedBy, 120),
            ResolvedAtUtc: incident.ResolvedAtUtc);
    }

    private static string ResolveIncidentStatus(CheckoutAlertIncident incident)
    {
        if (incident.ResolvedAtUtc.HasValue)
        {
            return "Resolved";
        }

        if (incident.IsAcknowledged)
        {
            return "Acknowledged";
        }

        return "Open";
    }

    private static decimal ResolveBreachDelta(decimal observed, decimal threshold, string comparator)
    {
        var delta = comparator switch
        {
            "below" => threshold - observed,
            _ => observed - threshold
        };

        if (delta < 0m)
        {
            return 0m;
        }

        return decimal.Round(delta, 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveComparator(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("success_drop", StringComparison.Ordinal) ||
            normalized.Contains("drop_submit_to_order", StringComparison.Ordinal))
        {
            return "below";
        }

        return "above";
    }

    private static string ResolveMetricUnit(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("latency", StringComparison.Ordinal))
        {
            return "ms";
        }

        if (normalized.Contains("failures", StringComparison.Ordinal))
        {
            return "count";
        }

        if (normalized.Contains("success_drop", StringComparison.Ordinal) ||
            normalized.Contains("provider_failure", StringComparison.Ordinal) ||
            normalized.Contains("drop_submit_to_order", StringComparison.Ordinal))
        {
            return "percent";
        }

        return "count";
    }

    private static string NormalizeCorrelationId(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Guid.NewGuid().ToString("N");
        }

        return normalized.Length <= 96 ? normalized : normalized[..96];
    }

    private static string Normalize(string? value, int maxLength, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = fallback;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
