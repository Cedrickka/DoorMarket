using DoorMarket.Api.Models;
using DoorMarket.Api.Services;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/checkout-observability")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminCheckoutObservabilityController : ControllerBase
{
    private readonly CheckoutObservabilityCalculator _calculator;
    private readonly ProductionObservabilityDashboardService _dashboard;
    private readonly CheckoutAlertingOptions _alertingOptions;
    private readonly DoorMarketDbContext _db;

    public AdminCheckoutObservabilityController(
        CheckoutObservabilityCalculator calculator,
        ProductionObservabilityDashboardService dashboard,
        IOptions<CheckoutAlertingOptions> alertingOptions,
        DoorMarketDbContext db)
    {
        _calculator = calculator;
        _dashboard = dashboard;
        _alertingOptions = alertingOptions.Value;
        _db = db;
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<CheckoutObservabilityCalculator.CheckoutSnapshotDto>> GetSnapshot(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 4,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        return Ok(await _calculator.GetSnapshotAsync(fromUtc, toUtc, ct));
    }

    [HttpGet("realtime")]
    public async Task<ActionResult<IReadOnlyList<CheckoutObservabilityCalculator.RealtimePointDto>>> GetRealtime(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 4,
        [FromQuery] int bucketMinutes = 5,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        return Ok(await _calculator.GetRealtimeSeriesAsync(fromUtc, toUtc, bucketMinutes, ct));
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IReadOnlyList<CheckoutObservabilityCalculator.PaymentQualityAlertDto>>> GetAlerts(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 4,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        return Ok(await _calculator.GetPaymentQualityAlertsAsync(fromUtc, toUtc, ct));
    }

    [HttpGet("alerts/payloads")]
    public async Task<ActionResult<IReadOnlyList<CheckoutPaymentIncidentPayload>>> GetAlertPayloads(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 4,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        var correlationId = ResolveCorrelationId();
        var generatedAtUtc = DateTime.UtcNow;
        var alerts = await _calculator.GetPaymentQualityAlertsAsync(fromUtc, toUtc, ct);
        var payloads = alerts
            .Select(x => CheckoutAlertPayloadFactory.FromAlert(x, correlationId, generatedAtUtc))
            .ToList();
        return Ok(payloads);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProductionObservabilityDashboardService.ProductionDashboardDto>> GetDashboard(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 24,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        return Ok(await _dashboard.GetDashboardAsync(fromUtc, toUtc, ct));
    }

    [HttpGet("slo")]
    public async Task<ActionResult<SloStatusDto>> GetSloStatus(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultHours = 4,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, defaultHours);
        var thresholds = BuildThresholds(_alertingOptions);
        var snapshot = await _calculator.GetSnapshotAsync(fromUtc, toUtc, ct, thresholds);
        var alerts = await _calculator.GetPaymentQualityAlertsAsync(fromUtc, toUtc, ct, thresholds);

        return Ok(new SloStatusDto(
            fromUtc,
            toUtc,
            DateTime.UtcNow,
            thresholds.PaymentSuccessCriticalThresholdPct,
            thresholds.PaymentSuccessWarningThresholdPct,
            thresholds.PaymentLatencyP95WarningMs,
            thresholds.SubmitToOrderWarningThresholdPct,
            thresholds.RedirectFailuresWarningCount,
            snapshot.PaymentSuccessRate,
            snapshot.LatencyP95Ms,
            snapshot.SubmitToOrderRate,
            snapshot.RedirectsFailed,
            alerts.Select(x => x.Code).ToList()));
    }

    [HttpGet("ab-ux")]
    public async Task<ActionResult<IReadOnlyList<CheckoutObservabilityCalculator.AbUxRowDto>>> GetAbUx(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultDays = 14,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRangeDays(from, to, defaultDays);
        return Ok(await _calculator.GetAbUxRowsAsync(fromUtc, toUtc, ct));
    }

    [HttpGet("incidents")]
    public async Task<ActionResult<IReadOnlyList<CheckoutAlertIncidentDto>>> GetIncidents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultDays = 14,
        [FromQuery] bool includeResolved = false,
        [FromQuery] bool includeAcknowledged = true,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var (fromUtc, toUtc) = NormalizeRangeDays(from, to, defaultDays);

        var query = _db.CheckoutAlertIncidents.AsNoTracking()
            .Where(x => x.LastTriggeredAtUtc >= fromUtc && x.LastTriggeredAtUtc < toUtc);

        if (!includeResolved)
        {
            query = query.Where(x => x.ResolvedAtUtc == null);
        }

        if (!includeAcknowledged)
        {
            query = query.Where(x => !x.IsAcknowledged);
        }

        var rows = await query
            .OrderBy(x => x.ResolvedAtUtc.HasValue ? 1 : 0)
            .ThenByDescending(x => x.Severity == "Critical")
            .ThenByDescending(x => x.LastTriggeredAtUtc)
            .Take(take)
            .Select(x => new CheckoutAlertIncidentDto(
                x.Id,
                x.AlertCode,
                x.Severity,
                x.Title,
                x.Description,
                x.Provider,
                x.ObservedValue,
                x.ThresholdValue,
                x.FirstTriggeredAtUtc,
                x.LastTriggeredAtUtc,
                x.TriggerCount,
                x.IsAcknowledged,
                x.AcknowledgedAtUtc,
                x.AcknowledgedBy,
                x.AcknowledgementNote,
                x.ResolvedAtUtc,
                x.LastNotifiedAtUtc,
                x.NotificationCount,
                x.LastNotificationError))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("incidents/payloads")]
    public async Task<ActionResult<IReadOnlyList<CheckoutPaymentIncidentPayload>>> GetIncidentPayloads(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int defaultDays = 14,
        [FromQuery] bool includeResolved = false,
        [FromQuery] bool includeAcknowledged = true,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var (fromUtc, toUtc) = NormalizeRangeDays(from, to, defaultDays);
        var correlationId = ResolveCorrelationId();
        var generatedAtUtc = DateTime.UtcNow;

        var query = _db.CheckoutAlertIncidents.AsNoTracking()
            .Where(x => x.LastTriggeredAtUtc >= fromUtc && x.LastTriggeredAtUtc < toUtc);

        if (!includeResolved)
        {
            query = query.Where(x => x.ResolvedAtUtc == null);
        }

        if (!includeAcknowledged)
        {
            query = query.Where(x => !x.IsAcknowledged);
        }

        var incidents = await query
            .OrderBy(x => x.ResolvedAtUtc.HasValue ? 1 : 0)
            .ThenByDescending(x => x.Severity == "Critical")
            .ThenByDescending(x => x.LastTriggeredAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var payloads = incidents
            .Select(x => CheckoutAlertPayloadFactory.FromIncident(x, correlationId, generatedAtUtc))
            .ToList();
        return Ok(payloads);
    }

    [HttpGet("incidents/{id:guid}/payload")]
    public async Task<ActionResult<CheckoutPaymentIncidentPayload>> GetIncidentPayload(
        Guid id,
        CancellationToken ct = default)
    {
        var incident = await _db.CheckoutAlertIncidents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null)
        {
            return NotFound("Incident introuvable.");
        }

        var payload = CheckoutAlertPayloadFactory.FromIncident(
            incident,
            ResolveCorrelationId(),
            DateTime.UtcNow);
        return Ok(payload);
    }

    [HttpPost("incidents/{id:guid}/acknowledge")]
    public async Task<ActionResult<CheckoutAlertIncidentDto>> AcknowledgeIncident(
        Guid id,
        [FromBody] IncidentAcknowledgeRequest? request,
        CancellationToken ct = default)
    {
        var incident = await _db.CheckoutAlertIncidents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null)
        {
            return NotFound("Incident introuvable.");
        }

        incident.IsAcknowledged = true;
        incident.AcknowledgedAtUtc = DateTime.UtcNow;
        incident.AcknowledgedBy = ResolveActor();
        incident.AcknowledgementNote = NormalizeNote(request?.Note);
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapIncident(incident));
    }

    [HttpPost("incidents/{id:guid}/reopen")]
    public async Task<ActionResult<CheckoutAlertIncidentDto>> ReopenIncident(
        Guid id,
        CancellationToken ct = default)
    {
        var incident = await _db.CheckoutAlertIncidents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null)
        {
            return NotFound("Incident introuvable.");
        }

        incident.IsAcknowledged = false;
        incident.AcknowledgedAtUtc = null;
        incident.AcknowledgedBy = null;
        incident.AcknowledgementNote = null;
        incident.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapIncident(incident));
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRange(DateTime? from, DateTime? to, int defaultHours)
    {
        defaultHours = Math.Clamp(defaultHours, 1, 24 * 14);
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddHours(-defaultHours)).ToUniversalTime();
        if (fromUtc >= toUtc)
        {
            fromUtc = toUtc.AddHours(-defaultHours);
        }

        return (fromUtc, toUtc);
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRangeDays(DateTime? from, DateTime? to, int defaultDays)
    {
        defaultDays = Math.Clamp(defaultDays, 1, 365);
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddDays(-defaultDays)).ToUniversalTime();
        if (fromUtc >= toUtc)
        {
            fromUtc = toUtc.AddDays(-defaultDays);
        }

        return (fromUtc, toUtc);
    }

    private CheckoutAlertIncidentDto MapIncident(Domain.Entities.CheckoutAlertIncident x)
        => new(
            x.Id,
            x.AlertCode,
            x.Severity,
            x.Title,
            x.Description,
            x.Provider,
            x.ObservedValue,
            x.ThresholdValue,
            x.FirstTriggeredAtUtc,
            x.LastTriggeredAtUtc,
            x.TriggerCount,
            x.IsAcknowledged,
            x.AcknowledgedAtUtc,
            x.AcknowledgedBy,
            x.AcknowledgementNote,
            x.ResolvedAtUtc,
            x.LastNotifiedAtUtc,
            x.NotificationCount,
            x.LastNotificationError);

    private string ResolveActor()
    {
        var name = User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        var sub = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(sub))
        {
            return sub.Trim();
        }

        return "admin";
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = (note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 600 ? normalized : normalized[..600];
    }

    private string ResolveCorrelationId()
    {
        var trace = HttpContext?.TraceIdentifier;
        if (string.IsNullOrWhiteSpace(trace))
        {
            return Guid.NewGuid().ToString("N");
        }

        return trace.Length <= 96 ? trace : trace[..96];
    }

    private static CheckoutObservabilityCalculator.AlertThresholds BuildThresholds(CheckoutAlertingOptions options)
    {
        return new CheckoutObservabilityCalculator.AlertThresholds(
            PaymentSuccessCriticalThresholdPct: Math.Clamp(options.PaymentSuccessCriticalThresholdPct, 0m, 100m),
            PaymentSuccessWarningThresholdPct: Math.Clamp(options.PaymentSuccessWarningThresholdPct, 0m, 100m),
            PaymentSuccessCriticalMinInitiatedCount: Math.Clamp(options.PaymentSuccessCriticalMinInitiatedCount, 1, 100_000),
            PaymentSuccessWarningMinInitiatedCount: Math.Clamp(options.PaymentSuccessWarningMinInitiatedCount, 1, 100_000),
            SubmitToOrderWarningThresholdPct: Math.Clamp(options.SubmitToOrderWarningThresholdPct, 0m, 100m),
            SubmitToOrderWarningMinSubmitCount: Math.Clamp(options.SubmitToOrderWarningMinSubmitCount, 1, 100_000),
            PaymentLatencyP95WarningMs: Math.Clamp(options.PaymentLatencyP95WarningMs, 250, 300_000),
            RedirectFailuresWarningCount: Math.Clamp(options.RedirectFailuresWarningCount, 1, 100_000),
            ProviderFailureCriticalThresholdPct: Math.Clamp(options.ProviderFailureCriticalThresholdPct, 0m, 100m),
            ProviderFailureWarningThresholdPct: Math.Clamp(options.ProviderFailureWarningThresholdPct, 0m, 100m),
            ProviderFailureMinInitiatedCount: Math.Clamp(options.ProviderFailureMinInitiatedCount, 1, 100_000));
    }

    public sealed record IncidentAcknowledgeRequest(string? Note);

    public sealed record CheckoutAlertIncidentDto(
        Guid Id,
        string AlertCode,
        string Severity,
        string Title,
        string Description,
        string? Provider,
        decimal ObservedValue,
        decimal ThresholdValue,
        DateTime FirstTriggeredAtUtc,
        DateTime LastTriggeredAtUtc,
        int TriggerCount,
        bool IsAcknowledged,
        DateTime? AcknowledgedAtUtc,
        string? AcknowledgedBy,
        string? AcknowledgementNote,
        DateTime? ResolvedAtUtc,
        DateTime? LastNotifiedAtUtc,
        int NotificationCount,
        string? LastNotificationError);

    public sealed record SloStatusDto(
        DateTime FromUtc,
        DateTime ToUtc,
        DateTime GeneratedAtUtc,
        decimal PaymentSuccessCriticalThresholdPct,
        decimal PaymentSuccessWarningThresholdPct,
        int PaymentLatencyP95WarningMs,
        decimal SubmitToOrderWarningThresholdPct,
        int RedirectFailuresWarningCount,
        decimal CurrentPaymentSuccessRatePct,
        int? CurrentPaymentLatencyP95Ms,
        decimal CurrentSubmitToOrderRatePct,
        int CurrentRedirectFailuresCount,
        IReadOnlyList<string> TriggeredAlertCodes);
}
