using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DoorMarket.Api.Services;

public sealed class CheckoutAlertingService
{
    private readonly DoorMarketDbContext _db;
    private readonly CheckoutObservabilityCalculator _calculator;
    private readonly CheckoutAlertingOptions _options;
    private readonly SupportEmailSender _supportEmailSender;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CheckoutAlertingService> _logger;

    public CheckoutAlertingService(
        DoorMarketDbContext db,
        CheckoutObservabilityCalculator calculator,
        IOptions<CheckoutAlertingOptions> options,
        SupportEmailSender supportEmailSender,
        IConfiguration config,
        ILogger<CheckoutAlertingService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _db = db;
        _calculator = calculator;
        _options = options.Value;
        _supportEmailSender = supportEmailSender;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<CheckoutAlertingRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new CheckoutAlertingRunResult(
                TriggeredAlerts: 0,
                OpenIncidents: 0,
                NewIncidents: 0,
                ResolvedIncidents: 0,
                NotifiedIncidents: 0,
                NotificationFailures: 0);
        }

        var now = DateTime.UtcNow;
        var runCorrelationId = $"checkout-alert-{Guid.NewGuid():N}";
        var analysisWindowMinutes = Math.Clamp(_options.AnalysisWindowMinutes, 5, 24 * 60 * 14);
        var fromUtc = now.AddMinutes(-analysisWindowMinutes);

        var thresholds = ResolveThresholds();
        var rawAlerts = await _calculator.GetPaymentQualityAlertsAsync(fromUtc, now, ct, thresholds);
        var alerts = rawAlerts
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_options.MaxIncidentsPerRun, 1, 200))
            .ToList();

        var activeIncidents = await _db.CheckoutAlertIncidents
            .Where(x => x.ResolvedAtUtc == null)
            .OrderByDescending(x => x.LastTriggeredAtUtc)
            .ToListAsync(ct);

        var incidentsByKey = activeIncidents
            .GroupBy(x => BuildIncidentKey(x.AlertCode, x.Provider))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.LastTriggeredAtUtc).First(),
                StringComparer.Ordinal);

        var touchedKeys = new HashSet<string>(StringComparer.Ordinal);
        var triggeredIncidents = new List<CheckoutAlertIncident>(alerts.Count);
        var newIncidents = 0;

        foreach (var alert in alerts)
        {
            var key = BuildIncidentKey(alert.Code, alert.Provider);
            touchedKeys.Add(key);

            if (!incidentsByKey.TryGetValue(key, out var incident))
            {
                incident = new CheckoutAlertIncident
                {
                    AlertCode = alert.Code,
                    Severity = Normalize(alert.Severity, 24, "Warning"),
                    Title = Normalize(alert.Title, 200, alert.Code),
                    Description = Normalize(alert.Description, 800, alert.Code),
                    Provider = NormalizeNullable(alert.Provider, 40),
                    ObservedValue = alert.ObservedValue,
                    ThresholdValue = alert.ThresholdValue,
                    FirstTriggeredAtUtc = now,
                    LastTriggeredAtUtc = now,
                    WindowFromUtc = alert.FromUtc,
                    WindowToUtc = alert.ToUtc,
                    TriggerCount = 1,
                    IsAcknowledged = false,
                    ResolvedAtUtc = null,
                    LastNotifiedAtUtc = null,
                    NotificationCount = 0
                };

                _db.CheckoutAlertIncidents.Add(incident);
                incidentsByKey[key] = incident;
                newIncidents++;
            }
            else
            {
                incident.Severity = Normalize(alert.Severity, 24, incident.Severity);
                incident.Title = Normalize(alert.Title, 200, incident.Title);
                incident.Description = Normalize(alert.Description, 800, incident.Description);
                incident.Provider = NormalizeNullable(alert.Provider, 40);
                incident.ObservedValue = alert.ObservedValue;
                incident.ThresholdValue = alert.ThresholdValue;
                incident.LastTriggeredAtUtc = now;
                incident.WindowFromUtc = alert.FromUtc;
                incident.WindowToUtc = alert.ToUtc;
                incident.TriggerCount = Math.Max(1, incident.TriggerCount + 1);
                incident.ResolvedAtUtc = null;
                incident.UpdatedAtUtc = now;
            }

            var payload = CheckoutAlertPayloadFactory.FromIncident(incident, runCorrelationId, now);
            _logger.LogWarning(
                "Checkout payment incident payload generated. code={Code} severity={Severity} provider={Provider} incidentId={IncidentId} correlationId={CorrelationId} payload={@Payload}",
                payload.AlertCode,
                payload.Severity,
                payload.Provider,
                payload.IncidentId,
                payload.CorrelationId,
                payload);

            triggeredIncidents.Add(incident);
        }

        var resolvedCount = 0;
        foreach (var incident in activeIncidents)
        {
            var key = BuildIncidentKey(incident.AlertCode, incident.Provider);
            if (touchedKeys.Contains(key))
            {
                continue;
            }

            incident.ResolvedAtUtc = now;
            incident.IsAcknowledged = false;
            incident.UpdatedAtUtc = now;
            resolvedCount++;
        }

        var notifyCandidates = triggeredIncidents
            .Where(x => !x.IsAcknowledged)
            .Where(x =>
            {
                if (!x.LastNotifiedAtUtc.HasValue)
                {
                    return true;
                }

                var cooldownMinutes = Math.Clamp(_options.NotifyCooldownMinutes, 1, 24 * 60 * 7);
                return x.LastNotifiedAtUtc.Value <= now.AddMinutes(-cooldownMinutes);
            })
            .DistinctBy(x => x.Id)
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenByDescending(x => x.LastTriggeredAtUtc)
            .ToList();

        var notifiedIncidents = 0;
        var notificationFailures = 0;
        var emailSentAny = false;
        var slackSent = false;
        var channelErrors = new List<string>();

        if (notifyCandidates.Count > 0)
        {
            var notifyPayloads = notifyCandidates
                .Select(x => CheckoutAlertPayloadFactory.FromIncident(x, runCorrelationId, now))
                .ToList();

            if (_options.NotifyByEmail)
            {
                var recipients = ResolveRecipients();
                if (recipients.Count == 0)
                {
                    channelErrors.Add("email_recipients_missing");
                    _logger.LogWarning("Checkout alerting email enabled but no recipients configured.");
                }
                else
                {
                    var subject = BuildEmailSubject(notifyCandidates);
                    var body = BuildEmailHtml(now, fromUtc, notifyPayloads);
                    string? lastError = null;

                    foreach (var recipient in recipients)
                    {
                        try
                        {
                            await _supportEmailSender.SendAsync(recipient, subject, body, ct);
                            emailSentAny = true;
                        }
                        catch (Exception ex)
                        {
                            lastError = ex.Message;
                            _logger.LogError(ex, "Checkout alerting email notification failed for recipient {Recipient}.", recipient);
                        }
                    }

                    if (!emailSentAny && !string.IsNullOrWhiteSpace(lastError))
                    {
                        channelErrors.Add($"email:{lastError}");
                    }
                }
            }

            if (_options.NotifyBySlack)
            {
                var (sent, error) = await SendSlackNotificationAsync(now, fromUtc, notifyPayloads, ct);
                slackSent = sent;
                if (!sent && !string.IsNullOrWhiteSpace(error))
                {
                    channelErrors.Add($"slack:{error}");
                }
            }

            var anyChannelSent = emailSentAny || slackSent;
            foreach (var incident in notifyCandidates)
            {
                incident.UpdatedAtUtc = now;
                if (anyChannelSent)
                {
                    incident.LastNotifiedAtUtc = now;
                    incident.NotificationCount = Math.Max(0, incident.NotificationCount) + 1;
                    incident.LastNotificationError = null;
                    notifiedIncidents++;
                }
                else
                {
                    incident.LastNotificationError = NormalizeNullable(string.Join(" | ", channelErrors), 1200);
                    notificationFailures++;
                }
            }

            foreach (var payload in notifyPayloads)
            {
                _logger.LogInformation(
                    "Checkout payment incident payload notified. code={Code} severity={Severity} provider={Provider} status={Status} correlationId={CorrelationId} viaEmail={ViaEmail} viaSlack={ViaSlack} payload={@Payload}",
                    payload.AlertCode,
                    payload.Severity,
                    payload.Provider,
                    payload.IncidentStatus,
                    payload.CorrelationId,
                    emailSentAny,
                    slackSent,
                    payload);
            }
        }

        await _db.SaveChangesAsync(ct);

        var openIncidents = await _db.CheckoutAlertIncidents
            .AsNoTracking()
            .CountAsync(x => x.ResolvedAtUtc == null, ct);

        return new CheckoutAlertingRunResult(
            TriggeredAlerts: alerts.Count,
            OpenIncidents: openIncidents,
            NewIncidents: newIncidents,
            ResolvedIncidents: resolvedCount,
            NotifiedIncidents: notifiedIncidents,
            NotificationFailures: notificationFailures,
            EmailSent: emailSentAny,
            SlackSent: slackSent);
    }

    private List<string> ResolveRecipients()
    {
        var raw = _options.AlertEmails;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = $"{_config["Support:Email"]};{_config["Admin:Email"]}";
        }

        return (raw ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private CheckoutObservabilityCalculator.AlertThresholds ResolveThresholds()
    {
        return new CheckoutObservabilityCalculator.AlertThresholds(
            PaymentSuccessCriticalThresholdPct: Math.Clamp(_options.PaymentSuccessCriticalThresholdPct, 0m, 100m),
            PaymentSuccessWarningThresholdPct: Math.Clamp(_options.PaymentSuccessWarningThresholdPct, 0m, 100m),
            PaymentSuccessCriticalMinInitiatedCount: Math.Clamp(_options.PaymentSuccessCriticalMinInitiatedCount, 1, 100_000),
            PaymentSuccessWarningMinInitiatedCount: Math.Clamp(_options.PaymentSuccessWarningMinInitiatedCount, 1, 100_000),
            SubmitToOrderWarningThresholdPct: Math.Clamp(_options.SubmitToOrderWarningThresholdPct, 0m, 100m),
            SubmitToOrderWarningMinSubmitCount: Math.Clamp(_options.SubmitToOrderWarningMinSubmitCount, 1, 100_000),
            PaymentLatencyP95WarningMs: Math.Clamp(_options.PaymentLatencyP95WarningMs, 250, 300_000),
            RedirectFailuresWarningCount: Math.Clamp(_options.RedirectFailuresWarningCount, 1, 100_000),
            ProviderFailureCriticalThresholdPct: Math.Clamp(_options.ProviderFailureCriticalThresholdPct, 0m, 100m),
            ProviderFailureWarningThresholdPct: Math.Clamp(_options.ProviderFailureWarningThresholdPct, 0m, 100m),
            ProviderFailureMinInitiatedCount: Math.Clamp(_options.ProviderFailureMinInitiatedCount, 1, 100_000));
    }

    private async Task<(bool Sent, string? Error)> SendSlackNotificationAsync(
        DateTime nowUtc,
        DateTime fromUtc,
        IReadOnlyList<DoorMarket.Api.Models.CheckoutPaymentIncidentPayload> payloads,
        CancellationToken ct)
    {
        if (payloads.Count == 0)
        {
            return (false, null);
        }

        var webhook = (_options.SlackWebhookUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(webhook))
        {
            _logger.LogWarning("Checkout alerting Slack enabled but webhook URL is missing.");
            return (false, "slack_webhook_missing");
        }

        var text = BuildSlackText(nowUtc, fromUtc, payloads);
        var payload = JsonSerializer.Serialize(new { text });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            if (_httpClientFactory is null)
            {
                using var localClient = new HttpClient();
                using var response = await localClient.PostAsync(webhook, content, ct);
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Checkout alerting Slack notification failed. status={StatusCode} body={Body}",
                    (int)response.StatusCode,
                    body);
                return (false, $"slack_http_{(int)response.StatusCode}");
            }
            else
            {
                using var response = await _httpClientFactory.CreateClient().PostAsync(webhook, content, ct);
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Checkout alerting Slack notification failed. status={StatusCode} body={Body}",
                    (int)response.StatusCode,
                    body);
                return (false, $"slack_http_{(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout alerting Slack notification failed.");
            return (false, ex.Message);
        }
    }

    private static string BuildEmailSubject(IReadOnlyList<CheckoutAlertIncident> incidents)
    {
        var critical = incidents.Count(x => string.Equals(x.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        var warning = incidents.Count - critical;
        return $"[DoorMarket][Checkout] Alerts actifs - critical: {critical}, warning: {warning}";
    }

    private static string BuildEmailHtml(DateTime nowUtc, DateTime fromUtc, IReadOnlyList<DoorMarket.Api.Models.CheckoutPaymentIncidentPayload> payloads)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>DoorMarket checkout alerts</h2>");
        sb.Append("<p>Run UTC: ").Append(nowUtc.ToString("yyyy-MM-dd HH:mm:ss")).Append("</p>");
        sb.Append("<p>Window start UTC: ").Append(fromUtc.ToString("yyyy-MM-dd HH:mm:ss")).Append("</p>");
        sb.Append("<p>Payload schema: checkout_payment_incident_v1</p>");
        sb.Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"6\">");
        sb.Append("<thead><tr>");
        sb.Append("<th>Status</th><th>Severity</th><th>Code</th><th>Provider</th><th>Observed</th><th>Threshold</th><th>Comparator</th><th>Description</th><th>CorrelationId</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var payload in payloads)
        {
            sb.Append("<tr>")
                .Append("<td>").Append(EscapeHtml(payload.IncidentStatus)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.Severity)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.AlertCode)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.Provider ?? "-")).Append("</td>")
                .Append("<td>").Append(payload.ObservedValue.ToString("0.##")).Append(' ').Append(EscapeHtml(payload.MetricUnit)).Append("</td>")
                .Append("<td>").Append(payload.ThresholdValue.ToString("0.##")).Append(' ').Append(EscapeHtml(payload.MetricUnit)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.ThresholdComparator)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.Description)).Append("</td>")
                .Append("<td>").Append(EscapeHtml(payload.CorrelationId)).Append("</td>")
                .Append("</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append("<p>Open admin dashboard for acknowledgement and triage. API payload endpoint: /api/admin/checkout-observability/alerts/payloads</p>");
        return sb.ToString();
    }

    private string BuildSlackText(
        DateTime nowUtc,
        DateTime fromUtc,
        IReadOnlyList<DoorMarket.Api.Models.CheckoutPaymentIncidentPayload> payloads)
    {
        var mention = (_options.SlackMention ?? string.Empty).Trim();
        var prefix = string.IsNullOrWhiteSpace(mention) ? string.Empty : $"{mention} ";
        var critical = payloads.Count(x => string.Equals(x.Severity, "Critical", StringComparison.OrdinalIgnoreCase));
        var warning = payloads.Count - critical;
        var lines = payloads
            .Take(8)
            .Select(x =>
                $"- [{x.Severity}] {x.AlertCode} provider={(string.IsNullOrWhiteSpace(x.Provider) ? "-" : x.Provider)} observed={x.ObservedValue:0.##}{x.MetricUnit} threshold={x.ThresholdValue:0.##}{x.MetricUnit}")
            .ToList();

        var body = new StringBuilder();
        body.Append(prefix)
            .Append("*DoorMarket checkout alerts*")
            .Append("\\nRun UTC: ").Append(nowUtc.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("\\nWindow from UTC: ").Append(fromUtc.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("\\nCritical: ").Append(critical).Append(" | Warning: ").Append(warning)
            .Append("\\nPayload schema: checkout_payment_incident_v1")
            .Append("\\n");

        foreach (var line in lines)
        {
            body.Append("\\n").Append(line);
        }

        if (payloads.Count > lines.Count)
        {
            body.Append("\\n... +").Append(payloads.Count - lines.Count).Append(" more incidents");
        }

        body.Append("\\nAPI: /api/admin/checkout-observability/alerts/payloads");
        return body.ToString();
    }

    private static string EscapeHtml(string value)
        => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string BuildIncidentKey(string code, string? provider)
        => $"{code.Trim().ToLowerInvariant()}::{(provider ?? string.Empty).Trim().ToLowerInvariant()}";

    private static int SeverityRank(string? severity)
        => string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

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

public sealed record CheckoutAlertingRunResult(
    int TriggeredAlerts,
    int OpenIncidents,
    int NewIncidents,
    int ResolvedIncidents,
    int NotifiedIncidents,
    int NotificationFailures,
    bool EmailSent = false,
    bool SlackSent = false);
