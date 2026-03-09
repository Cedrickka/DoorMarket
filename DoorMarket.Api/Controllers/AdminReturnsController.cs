using System.Text;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Returns;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/returns")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminReturnsController : ControllerBase
{
    private const string StatusRequested = "Requested";
    private const string StatusApproved = "Approved";
    private const string StatusRejected = "Rejected";
    private const string StatusRefunded = "Refunded";
    private const int MaxAdminNoteLength = 1000;

    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public AdminReturnsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("reasons")]
    public async Task<ActionResult<IReadOnlyList<ReturnReasonDto>>> GetReasons(CancellationToken ct = default)
    {
        var rows = await _db.ReturnReasons.AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .Select(r => new ReturnReasonDto(
                r.Code,
                r.TitleFr,
                r.TitleEn,
                r.DescriptionFr,
                r.DescriptionEn,
                r.DefaultSlaHours,
                r.SortOrder))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminReturnRow>>> GetReturns(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] bool? slaBreached = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var normalizedStatus = NormalizeStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status de retour invalide.");
        }

        var query = BuildFilteredQuery(normalizedStatus, from, to, q, slaBreached);
        var total = await query.CountAsync(ct);
        var nowUtc = DateTime.UtcNow;

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminReturnRow(
                r.Id,
                r.OrderId,
                BuildOrderCode(r.OrderId),
                r.Status,
                r.ReasonCode,
                r.Reason,
                r.RequestedAmount,
                r.ApprovedAmount,
                r.Currency,
                r.CreatedAtUtc,
                r.ReviewedAtUtc,
                r.RefundedAtUtc,
                r.SlaTargetAtUtc,
                r.LastStatusChangedAtUtc,
                r.SlaTargetAtUtc.HasValue &&
                (r.Status == StatusRequested || r.Status == StatusApproved) &&
                r.SlaTargetAtUtc.Value < nowUtc,
                r.Order.DeliveryName,
                r.Order.User != null ? r.Order.User.Email : null))
            .ToListAsync(ct);

        return Ok(new PagedResult<AdminReturnRow>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] bool? slaBreached = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status de retour invalide.");
        }

        var nowUtc = DateTime.UtcNow;
        var rows = await BuildFilteredQuery(normalizedStatus, from, to, q, slaBreached)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new
            {
                r.Id,
                r.OrderId,
                r.Status,
                r.ReasonCode,
                r.Reason,
                r.RequestedAmount,
                r.ApprovedAmount,
                r.Currency,
                r.CreatedAtUtc,
                r.ReviewedAtUtc,
                r.RefundedAtUtc,
                r.SlaTargetAtUtc,
                r.LastStatusChangedAtUtc,
                IsSlaBreached = r.SlaTargetAtUtc.HasValue &&
                                (r.Status == StatusRequested || r.Status == StatusApproved) &&
                                r.SlaTargetAtUtc.Value < nowUtc,
                CustomerName = r.Order.DeliveryName,
                CustomerEmail = r.Order.User != null ? r.Order.User.Email : null
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("returnId,orderId,orderNumber,status,reasonCode,reason,requestedAmount,approvedAmount,currency,createdAtUtc,reviewedAtUtc,refundedAtUtc,slaTargetAtUtc,lastStatusChangedAtUtc,isSlaBreached,customerName,customerEmail");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(row.Id),
                Csv(row.OrderId),
                Csv(BuildOrderCode(row.OrderId)),
                Csv(row.Status),
                Csv(row.ReasonCode),
                Csv(row.Reason),
                Csv(row.RequestedAmount),
                Csv(row.ApprovedAmount),
                Csv(row.Currency),
                Csv(row.CreatedAtUtc),
                Csv(row.ReviewedAtUtc),
                Csv(row.RefundedAtUtc),
                Csv(row.SlaTargetAtUtc),
                Csv(row.LastStatusChangedAtUtc),
                Csv(row.IsSlaBreached),
                Csv(row.CustomerName),
                Csv(row.CustomerEmail)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"returns_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminReturnDetail>> GetById(Guid id, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var row = await _db.ReturnRequests.AsNoTracking()
            .Include(r => r.Order)
            .ThenInclude(o => o.User)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.Id == id)
            .Select(r => new AdminReturnDetail(
                r.Id,
                r.OrderId,
                BuildOrderCode(r.OrderId),
                r.Status,
                r.ReasonCode,
                r.Reason,
                r.Comment,
                r.RequestedAmount,
                r.ApprovedAmount,
                r.Currency,
                r.AdminNote,
                r.CreatedAtUtc,
                r.ReviewedAtUtc,
                r.RefundedAtUtc,
                r.SlaTargetAtUtc,
                r.LastStatusChangedAtUtc,
                r.SlaTargetAtUtc.HasValue &&
                (r.Status == StatusRequested || r.Status == StatusApproved) &&
                r.SlaTargetAtUtc.Value < nowUtc,
                r.Order.DeliveryName,
                r.Order.DeliveryPhone,
                r.Order.User != null ? r.Order.User.Email : null,
                r.ReviewedByUser != null ? r.ReviewedByUser.Email : null))
            .FirstOrDefaultAsync(ct);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<AdminReturnHistoryRow>>> GetHistory(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.ReturnRequests.AsNoTracking().AnyAsync(r => r.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        var rows = await _db.ReturnRequestStatusHistories.AsNoTracking()
            .Include(x => x.ChangedByUser)
            .Where(x => x.ReturnRequestId == id)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new AdminReturnHistoryRow(
                x.Id,
                x.OldStatus,
                x.NewStatus,
                x.Note,
                x.ChangedAtUtc,
                x.ChangedByUser != null ? x.ChangedByUser.Email : null))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdminReturnDetail>> Approve(
        Guid id,
        [FromBody] ApproveReturnRequest req,
        CancellationToken ct = default)
    {
        var row = await _db.ReturnRequests
            .Include(r => r.Order)
            .ThenInclude(o => o.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (row is null)
        {
            return NotFound();
        }

        if (!string.Equals(row.Status, StatusRequested, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(row.Status, StatusApproved, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ToDetail(row));
            }

            return Conflict("Transition invalide: seul un retour Requested peut etre approuve.");
        }

        var approvedAmount = req.ApprovedAmount ?? row.RequestedAmount;
        approvedAmount = decimal.Round(approvedAmount, 2, MidpointRounding.AwayFromZero);
        if (approvedAmount <= 0m)
        {
            return BadRequest("ApprovedAmount doit etre superieur a 0.");
        }

        if (approvedAmount > row.RequestedAmount)
        {
            return BadRequest("ApprovedAmount ne peut pas depasser RequestedAmount.");
        }

        var now = DateTime.UtcNow;
        var note = NormalizeAdminNote(req.AdminNote);
        var oldStatus = row.Status;
        row.Status = StatusApproved;
        row.ApprovedAmount = approvedAmount;
        row.ReviewedAtUtc = now;
        row.ReviewedByUserId = _current.UserId;
        row.LastStatusChangedAtUtc = now;
        row.AdminNote = note;
        row.UpdatedAtUtc = now;

        _db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
        {
            ReturnRequestId = row.Id,
            OldStatus = oldStatus,
            NewStatus = StatusApproved,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = now,
            Note = note ?? "Return approved by admin."
        });

        await _db.SaveChangesAsync(ct);
        return Ok(ToDetail(row));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AdminReturnDetail>> Reject(
        Guid id,
        [FromBody] RejectReturnRequest req,
        CancellationToken ct = default)
    {
        var row = await _db.ReturnRequests
            .Include(r => r.Order)
            .ThenInclude(o => o.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (row is null)
        {
            return NotFound();
        }

        if (!string.Equals(row.Status, StatusRequested, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(row.Status, StatusRejected, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ToDetail(row));
            }

            return Conflict("Transition invalide: seul un retour Requested peut etre rejete.");
        }

        var now = DateTime.UtcNow;
        var note = NormalizeAdminNote(req.AdminNote);
        var oldStatus = row.Status;
        row.Status = StatusRejected;
        row.ApprovedAmount = 0m;
        row.ReviewedAtUtc = now;
        row.ReviewedByUserId = _current.UserId;
        row.LastStatusChangedAtUtc = now;
        row.AdminNote = note;
        row.UpdatedAtUtc = now;

        _db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
        {
            ReturnRequestId = row.Id,
            OldStatus = oldStatus,
            NewStatus = StatusRejected,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = now,
            Note = note ?? "Return rejected by admin."
        });

        await _db.SaveChangesAsync(ct);
        return Ok(ToDetail(row));
    }

    [HttpPost("{id:guid}/mark-refunded")]
    public async Task<ActionResult<AdminReturnDetail>> MarkRefunded(
        Guid id,
        [FromBody] MarkReturnRefundedRequest req,
        CancellationToken ct = default)
    {
        var row = await _db.ReturnRequests
            .Include(r => r.Order)
            .ThenInclude(o => o.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (row is null)
        {
            return NotFound();
        }

        if (!string.Equals(row.Status, StatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(row.Status, StatusRefunded, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ToDetail(row));
            }

            return Conflict("Transition invalide: seul un retour Approved peut etre marque rembourse.");
        }

        if (req.ApprovedAmount.HasValue)
        {
            var updatedApproved = decimal.Round(req.ApprovedAmount.Value, 2, MidpointRounding.AwayFromZero);
            if (updatedApproved <= 0m)
            {
                return BadRequest("ApprovedAmount doit etre superieur a 0.");
            }

            if (updatedApproved > row.RequestedAmount)
            {
                return BadRequest("ApprovedAmount ne peut pas depasser RequestedAmount.");
            }

            row.ApprovedAmount = updatedApproved;
        }
        else if (!row.ApprovedAmount.HasValue || row.ApprovedAmount <= 0m)
        {
            row.ApprovedAmount = row.RequestedAmount;
        }

        var now = DateTime.UtcNow;
        var refundedAt = req.RefundedAtUtc?.ToUniversalTime() ?? now;
        var note = NormalizeAdminNote(req.AdminNote);

        var oldStatus = row.Status;
        row.Status = StatusRefunded;
        row.RefundedAtUtc = refundedAt;
        row.LastStatusChangedAtUtc = now;
        row.ReviewedAtUtc ??= now;
        row.ReviewedByUserId ??= _current.UserId;
        row.AdminNote = note ?? row.AdminNote;
        row.UpdatedAtUtc = now;

        _db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
        {
            ReturnRequestId = row.Id,
            OldStatus = oldStatus,
            NewStatus = StatusRefunded,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = now,
            Note = note ?? "Return marked as refunded by admin."
        });

        await _db.SaveChangesAsync(ct);
        return Ok(ToDetail(row));
    }

    private IQueryable<ReturnRequest> BuildFilteredQuery(
        string? normalizedStatus,
        DateTime? from,
        DateTime? to,
        string? q,
        bool? slaBreached)
    {
        var query = _db.ReturnRequests.AsNoTracking()
            .Include(r => r.Order)
            .ThenInclude(o => o.User)
            .AsQueryable();

        if (normalizedStatus is not null)
        {
            query = query.Where(r => r.Status == normalizedStatus);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToUniversalTime();
            query = query.Where(r => r.CreatedAtUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            if (toUtc.TimeOfDay == TimeSpan.Zero)
            {
                toUtc = toUtc.AddDays(1);
            }

            query = query.Where(r => r.CreatedAtUtc < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(r =>
                r.Order.DeliveryName.Contains(s) ||
                r.Order.DeliveryPhone.Contains(s) ||
                (r.Order.User != null && r.Order.User.Email.Contains(s)));
        }

        if (slaBreached.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            if (slaBreached.Value)
            {
                query = query.Where(r =>
                    r.SlaTargetAtUtc.HasValue &&
                    (r.Status == StatusRequested || r.Status == StatusApproved) &&
                    r.SlaTargetAtUtc.Value < nowUtc);
            }
            else
            {
                query = query.Where(r =>
                    !r.SlaTargetAtUtc.HasValue ||
                    r.SlaTargetAtUtc.Value >= nowUtc ||
                    (r.Status != StatusRequested && r.Status != StatusApproved));
            }
        }

        return query;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var value = status.Trim();
        if (string.Equals(value, StatusRequested, StringComparison.OrdinalIgnoreCase))
        {
            return StatusRequested;
        }

        if (string.Equals(value, StatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            return StatusApproved;
        }

        if (string.Equals(value, StatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            return StatusRejected;
        }

        if (string.Equals(value, StatusRefunded, StringComparison.OrdinalIgnoreCase))
        {
            return StatusRefunded;
        }

        return null;
    }

    private static string? NormalizeAdminNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxAdminNoteLength)
        {
            return normalized[..MaxAdminNoteLength];
        }

        return normalized;
    }

    private static AdminReturnDetail ToDetail(ReturnRequest r)
    {
        var nowUtc = DateTime.UtcNow;
        return new AdminReturnDetail(
            r.Id,
            r.OrderId,
            BuildOrderCode(r.OrderId),
            r.Status,
            r.ReasonCode,
            r.Reason,
            r.Comment,
            r.RequestedAmount,
            r.ApprovedAmount,
            r.Currency,
            r.AdminNote,
            r.CreatedAtUtc,
            r.ReviewedAtUtc,
            r.RefundedAtUtc,
            r.SlaTargetAtUtc,
            r.LastStatusChangedAtUtc,
            r.SlaTargetAtUtc.HasValue &&
            (r.Status == StatusRequested || r.Status == StatusApproved) &&
            r.SlaTargetAtUtc.Value < nowUtc,
            r.Order.DeliveryName,
            r.Order.DeliveryPhone,
            r.Order.User?.Email,
            r.ReviewedByUser?.Email);
    }

    private static string BuildOrderCode(Guid orderId)
        => $"DM{orderId.ToString("N")[..6].ToUpperInvariant()}";

    private static string Csv(object? value)
    {
        if (value is null)
        {
            return "";
        }

        var text = value switch
        {
            DateTime dt => dt.ToUniversalTime().ToString("O"),
            decimal d => d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? ""
        };

        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public sealed record ApproveReturnRequest(decimal? ApprovedAmount, string? AdminNote);
    public sealed record RejectReturnRequest(string? AdminNote);
    public sealed record MarkReturnRefundedRequest(decimal? ApprovedAmount, DateTime? RefundedAtUtc, string? AdminNote);

    public sealed record AdminReturnRow(
        Guid Id,
        Guid OrderId,
        string OrderNumber,
        string Status,
        string? ReasonCode,
        string Reason,
        decimal RequestedAmount,
        decimal? ApprovedAmount,
        string Currency,
        DateTime CreatedAtUtc,
        DateTime? ReviewedAtUtc,
        DateTime? RefundedAtUtc,
        DateTime? SlaTargetAtUtc,
        DateTime? LastStatusChangedAtUtc,
        bool IsSlaBreached,
        string CustomerName,
        string? CustomerEmail);

    public sealed record AdminReturnDetail(
        Guid Id,
        Guid OrderId,
        string OrderNumber,
        string Status,
        string? ReasonCode,
        string Reason,
        string? Comment,
        decimal RequestedAmount,
        decimal? ApprovedAmount,
        string Currency,
        string? AdminNote,
        DateTime CreatedAtUtc,
        DateTime? ReviewedAtUtc,
        DateTime? RefundedAtUtc,
        DateTime? SlaTargetAtUtc,
        DateTime? LastStatusChangedAtUtc,
        bool IsSlaBreached,
        string CustomerName,
        string CustomerPhone,
        string? CustomerEmail,
        string? ReviewedByEmail);

    public sealed record AdminReturnHistoryRow(
        Guid Id,
        string OldStatus,
        string NewStatus,
        string? Note,
        DateTime ChangedAtUtc,
        string? ChangedByEmail);
}
