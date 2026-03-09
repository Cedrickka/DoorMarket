using DoorMarket.Api.Services;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminNotificationsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly INotificationReplayService _replay;

    public AdminNotificationsController(DoorMarketDbContext db, INotificationReplayService replay)
    {
        _db = db;
        _replay = replay;
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<TransactionsPageDto>> GetTransactions(
        [FromQuery] Guid? orderId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildTransactionsFilteredQuery(orderId, type, status, from, to);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionNotificationDto(
                x.Id,
                x.OrderId,
                x.NotificationType,
                x.Channel,
                x.Recipient,
                x.Subject,
                x.Status,
                x.Error,
                x.AttemptedAtUtc))
            .ToListAsync(ct);

        return Ok(new TransactionsPageDto(page, pageSize, total, rows));
    }

    [HttpGet("transactions/incidents")]
    public async Task<ActionResult<IReadOnlyList<NotificationIncidentDto>>> GetIncidents(
        [FromQuery] Guid? orderId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int minFailures = 1,
        [FromQuery] bool includeAcknowledged = false,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        minFailures = Math.Clamp(minFailures, 1, 100);
        take = Math.Clamp(take, 1, 200);

        var query = BuildTransactionsFilteredQuery(orderId, type, NotificationEvents.StatusFailed, from, to);

        // Unresolved failure: no newer successful attempt for same order/type/recipient.
        var unresolvedRows = await query
            .Where(f => !_db.TransactionalNotificationLogs.Any(s =>
                s.Status == NotificationEvents.StatusSent &&
                s.NotificationType == f.NotificationType &&
                s.Recipient == f.Recipient &&
                ((s.OrderId == f.OrderId) || (!s.OrderId.HasValue && !f.OrderId.HasValue)) &&
                s.AttemptedAtUtc > f.AttemptedAtUtc))
            .OrderByDescending(x => x.AttemptedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.NotificationType,
                x.Recipient,
                x.Error,
                x.AttemptedAtUtc
            })
            .ToListAsync(ct);

        var activeAcknowledgements = await _db.NotificationIncidentAcknowledgements
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.OrderId,
                x.NotificationType,
                x.Recipient,
                x.AcknowledgedBy,
                x.AcknowledgedAtUtc,
                x.Note
            })
            .ToListAsync(ct);

        var acknowledgementByKey = activeAcknowledgements
            .GroupBy(x => BuildIncidentKey(x.OrderId, x.NotificationType, x.Recipient))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.AcknowledgedAtUtc).First());

        var incidents = unresolvedRows
            .GroupBy(x => new { x.OrderId, x.NotificationType, x.Recipient })
            .Select(g =>
            {
                var last = g.OrderByDescending(x => x.AttemptedAtUtc).First();
                var first = g.OrderBy(x => x.AttemptedAtUtc).First();
                var key = BuildIncidentKey(g.Key.OrderId, g.Key.NotificationType, g.Key.Recipient);
                var acknowledged = acknowledgementByKey.TryGetValue(key, out var ack);
                return new NotificationIncidentDto(
                    last.Id,
                    g.Key.OrderId,
                    g.Key.NotificationType,
                    g.Key.Recipient,
                    g.Count(),
                    first.AttemptedAtUtc,
                    last.AttemptedAtUtc,
                    last.Error,
                    acknowledged,
                    ack?.AcknowledgedAtUtc,
                    ack?.AcknowledgedBy,
                    ack?.Note);
            })
            .Where(x => x.FailedCount >= minFailures)
            .Where(x => includeAcknowledged || !x.Acknowledged)
            .OrderByDescending(x => x.LastAttemptUtc)
            .Take(take)
            .ToList();

        return Ok(incidents);
    }

    [HttpPost("transactions/incidents/{lastLogId:guid}/acknowledge")]
    public async Task<ActionResult<IncidentAcknowledgeDto>> AcknowledgeIncident(
        Guid lastLogId,
        [FromBody] IncidentAcknowledgeRequest? request,
        CancellationToken ct = default)
    {
        var target = await _db.TransactionalNotificationLogs
            .AsNoTracking()
            .Where(x => x.Id == lastLogId)
            .Select(x => new { x.Id, x.OrderId, x.NotificationType, x.Recipient })
            .FirstOrDefaultAsync(ct);

        if (target is null)
        {
            return NotFound("Notification introuvable.");
        }

        var note = NormalizeNote(request?.Note);
        var actor = ResolveActor();
        var now = DateTime.UtcNow;

        var ack = await _db.NotificationIncidentAcknowledgements.FirstOrDefaultAsync(x =>
            x.NotificationType == target.NotificationType &&
            x.Recipient == target.Recipient &&
            ((x.OrderId == target.OrderId) || (!x.OrderId.HasValue && !target.OrderId.HasValue)), ct);

        if (ack is null)
        {
            ack = new Domain.Entities.NotificationIncidentAcknowledgement
            {
                OrderId = target.OrderId,
                NotificationType = target.NotificationType,
                Recipient = target.Recipient,
                LastLogId = target.Id
            };
            _db.NotificationIncidentAcknowledgements.Add(ack);
        }

        ack.IsActive = true;
        ack.LastLogId = target.Id;
        ack.AcknowledgedAtUtc = now;
        ack.AcknowledgedBy = actor;
        ack.Note = note;
        ack.ReopenedAtUtc = null;
        ack.ReopenedBy = null;

        await _db.SaveChangesAsync(ct);

        return Ok(new IncidentAcknowledgeDto(
            ack.LastLogId,
            ack.OrderId,
            ack.NotificationType,
            ack.Recipient,
            ack.IsActive,
            ack.AcknowledgedAtUtc,
            ack.AcknowledgedBy,
            ack.Note));
    }

    [HttpPost("transactions/incidents/{lastLogId:guid}/reopen")]
    public async Task<ActionResult<IncidentAcknowledgeDto>> ReopenIncident(
        Guid lastLogId,
        CancellationToken ct = default)
    {
        var target = await _db.TransactionalNotificationLogs
            .AsNoTracking()
            .Where(x => x.Id == lastLogId)
            .Select(x => new { x.Id, x.OrderId, x.NotificationType, x.Recipient })
            .FirstOrDefaultAsync(ct);

        if (target is null)
        {
            return NotFound("Notification introuvable.");
        }

        var ack = await _db.NotificationIncidentAcknowledgements.FirstOrDefaultAsync(x =>
            x.NotificationType == target.NotificationType &&
            x.Recipient == target.Recipient &&
            ((x.OrderId == target.OrderId) || (!x.OrderId.HasValue && !target.OrderId.HasValue)), ct);

        if (ack is null || !ack.IsActive)
        {
            return NotFound("Incident non acknowledge.");
        }

        ack.IsActive = false;
        ack.ReopenedAtUtc = DateTime.UtcNow;
        ack.ReopenedBy = ResolveActor();
        await _db.SaveChangesAsync(ct);

        return Ok(new IncidentAcknowledgeDto(
            ack.LastLogId,
            ack.OrderId,
            ack.NotificationType,
            ack.Recipient,
            ack.IsActive,
            ack.AcknowledgedAtUtc,
            ack.AcknowledgedBy,
            ack.Note));
    }

    [HttpGet("transactions/export.csv")]
    public async Task<IActionResult> ExportTransactionsCsv(
        [FromQuery] Guid? orderId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxRows = 5000,
        CancellationToken ct = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 20000);
        var rows = await BuildTransactionsFilteredQuery(orderId, type, status, from, to)
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(maxRows)
            .Select(x => new
            {
                x.Id,
                x.OrderId,
                x.NotificationType,
                x.Channel,
                x.Recipient,
                x.Subject,
                x.Status,
                x.Error,
                x.AttemptedAtUtc
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id,OrderId,NotificationType,Channel,Recipient,Subject,Status,Error,AttemptedAtUtc");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(row.Id),
                Csv(row.OrderId),
                Csv(row.NotificationType),
                Csv(row.Channel),
                Csv(row.Recipient),
                Csv(row.Subject),
                Csv(row.Status),
                Csv(row.Error),
                Csv(row.AttemptedAtUtc)));
        }

        var fileName = $"notification-transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("transactions/summary")]
    public async Task<ActionResult<IReadOnlyList<TransactionNotificationSummaryDto>>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var query = _db.TransactionalNotificationLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.AttemptedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.AttemptedAtUtc < toUtc.Value);
        }

        var rows = await query
            .GroupBy(x => x.NotificationType)
            .Select(g => new
            {
                NotificationType = g.Key,
                Total = g.Count(),
                Sent = g.Sum(x => x.Status == NotificationEvents.StatusSent ? 1 : 0),
                Failed = g.Sum(x => x.Status == NotificationEvents.StatusFailed ? 1 : 0),
                LastAttemptUtc = g.Max(x => (DateTime?)x.AttemptedAtUtc)
            })
            .OrderBy(x => x.NotificationType)
            .ToListAsync(ct);

        var result = rows
            .Select(x => new TransactionNotificationSummaryDto(
                x.NotificationType,
                x.Total,
                x.Sent,
                x.Failed,
                x.LastAttemptUtc))
            .ToList();

        return Ok(result);
    }

    [HttpPost("transactions/{id:guid}/retry")]
    public async Task<ActionResult<RetryTransactionDto>> RetryTransaction(Guid id, CancellationToken ct = default)
    {
        var result = await _replay.RetryAsync(id, ct);
        if (!result.Found)
        {
            return NotFound("Notification introuvable.");
        }

        return Ok(new RetryTransactionDto(
            id,
            result.Retried,
            result.Outcome,
            result.Message,
            result.OrderId,
            result.NotificationType));
    }

    [HttpPost("transactions/retry-failed")]
    public async Task<ActionResult<BulkRetryTransactionsDto>> RetryFailedTransactions(
        [FromQuery] int limit = 25,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var normalizedType = NormalizeFilter(type);

        var failedQuery = _db.TransactionalNotificationLogs.AsNoTracking()
            .Where(x => x.Status == NotificationEvents.StatusFailed);

        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            failedQuery = failedQuery.Where(x => x.NotificationType == normalizedType);
        }

        // Retry only unresolved failures: no newer successful attempt for same order/type/recipient.
        var candidateIds = await failedQuery
            .Where(f => !_db.TransactionalNotificationLogs.Any(s =>
                ((s.OrderId == f.OrderId) || (!s.OrderId.HasValue && !f.OrderId.HasValue)) &&
                s.NotificationType == f.NotificationType &&
                s.Recipient == f.Recipient &&
                s.Status == NotificationEvents.StatusSent &&
                s.AttemptedAtUtc > f.AttemptedAtUtc))
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);

        var totalCandidates = candidateIds.Count;
        var triggered = 0;
        var ignored = 0;
        var failed = 0;

        foreach (var id in candidateIds)
        {
            var retry = await _replay.RetryAsync(id, ct);
            if (retry.Retried)
            {
                triggered++;
            }
            else if (retry.Outcome == "ReplayError")
            {
                failed++;
            }
            else
            {
                ignored++;
            }
        }

        return Ok(new BulkRetryTransactionsDto(
            totalCandidates,
            triggered,
            ignored,
            failed));
    }

    private IQueryable<Domain.Entities.TransactionalNotificationLog> BuildTransactionsFilteredQuery(
        Guid? orderId,
        string? type,
        string? status,
        DateTime? from,
        DateTime? to)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var normalizedType = NormalizeFilter(type);
        var normalizedStatus = NormalizeFilter(status);

        var query = _db.TransactionalNotificationLogs.AsNoTracking().AsQueryable();

        if (orderId.HasValue && orderId.Value != Guid.Empty)
        {
            query = query.Where(x => x.OrderId == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            query = query.Where(x => x.NotificationType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.AttemptedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.AttemptedAtUtc < toUtc.Value);
        }

        return query;
    }

    private static string Csv(object? value)
    {
        if (value is null)
        {
            return "\"\"";
        }

        var s = value switch
        {
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

        s = s.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }

    private static string? NormalizeFilter(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = (note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > 500 ? normalized[..500] : normalized;
    }

    private static string BuildIncidentKey(Guid? orderId, string notificationType, string recipient)
        => $"{orderId?.ToString("N") ?? "none"}::{notificationType.Trim()}::{recipient.Trim().ToLowerInvariant()}";

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

    private static (DateTime? fromUtc, DateTime? toUtc) NormalizeRange(DateTime? from, DateTime? to)
    {
        DateTime? fromUtc = from?.ToUniversalTime();
        DateTime? toUtc = to?.ToUniversalTime();

        if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return (fromUtc, toUtc);
    }

    public sealed record TransactionsPageDto(
        int Page,
        int PageSize,
        int Total,
        IReadOnlyList<TransactionNotificationDto> Items);

    public sealed record TransactionNotificationDto(
        Guid Id,
        Guid? OrderId,
        string NotificationType,
        string Channel,
        string Recipient,
        string Subject,
        string Status,
        string? Error,
        DateTime AttemptedAtUtc);

    public sealed record TransactionNotificationSummaryDto(
        string NotificationType,
        int Total,
        int Sent,
        int Failed,
        DateTime? LastAttemptUtc);

    public sealed record NotificationIncidentDto(
        Guid LastLogId,
        Guid? OrderId,
        string NotificationType,
        string Recipient,
        int FailedCount,
        DateTime FirstAttemptUtc,
        DateTime LastAttemptUtc,
        string? LastError,
        bool Acknowledged,
        DateTime? AcknowledgedAtUtc,
        string? AcknowledgedBy,
        string? AcknowledgementNote);

    public sealed record IncidentAcknowledgeRequest(string? Note);

    public sealed record IncidentAcknowledgeDto(
        Guid LastLogId,
        Guid? OrderId,
        string NotificationType,
        string Recipient,
        bool IsActive,
        DateTime AcknowledgedAtUtc,
        string AcknowledgedBy,
        string? Note);

    public sealed record RetryTransactionDto(
        Guid NotificationLogId,
        bool Retried,
        string Outcome,
        string Message,
        Guid? OrderId,
        string? NotificationType);

    public sealed record BulkRetryTransactionsDto(
        int Candidates,
        int Triggered,
        int Ignored,
        int Failed);
}
