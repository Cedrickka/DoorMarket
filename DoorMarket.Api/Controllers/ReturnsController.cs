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
[Route("api/returns")]
[Authorize]
public class ReturnsController : ControllerBase
{
    private const int MaxReasonLength = 120;
    private const int MaxCommentLength = 1000;
    private const int DefaultSlaHours = 72;
    private const string StatusRequested = "Requested";
    private const string StatusApproved = "Approved";
    private const string StatusRejected = "Rejected";
    private const string StatusRefunded = "Refunded";
    private const string StatusCancelled = "Cancelled";

    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public ReturnsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("reasons")]
    public async Task<ActionResult<IReadOnlyList<ReturnReasonDto>>> GetReasons(CancellationToken ct = default)
    {
        var rows = await _db.ReturnReasons.AsNoTracking()
            .Where(r => r.IsActive)
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

    [HttpPost]
    public async Task<ActionResult<ReturnRequestDto>> Create(
        [FromBody] CreateReturnRequest req,
        CancellationToken ct = default)
    {
        if (req.OrderId == Guid.Empty)
        {
            return BadRequest("OrderId requis.");
        }

        var normalizedReasonCode = NormalizeReasonCode(req.ReasonCode);
        ReturnReason? reasonDef = null;
        if (normalizedReasonCode is not null)
        {
            reasonDef = await _db.ReturnReasons.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == normalizedReasonCode && r.IsActive, ct);
            if (reasonDef is null)
            {
                return BadRequest("ReasonCode invalide.");
            }
        }

        var reason = reasonDef is not null ? reasonDef.TitleFr : req.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest("La raison du retour est requise.");
        }

        if (reason.Length > MaxReasonLength)
        {
            return BadRequest($"La raison ne doit pas depasser {MaxReasonLength} caracteres.");
        }

        var comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        if (comment is not null && comment.Length > MaxCommentLength)
        {
            return BadRequest($"Le commentaire ne doit pas depasser {MaxCommentLength} caracteres.");
        }

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == req.OrderId && o.UserId == userId)
            .Select(o => new
            {
                o.Id,
                o.PaymentStatus,
                o.FulfillmentStatus,
                o.Status,
                o.TotalAmount,
                o.Currency
            })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return NotFound("Commande introuvable.");
        }

        if (!string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Seules les commandes payees peuvent etre retournees.");
        }

        var deliveredOrCompleted =
            string.Equals(order.FulfillmentStatus, "Delivered", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        if (!deliveredOrCompleted)
        {
            return BadRequest("La commande doit etre livree pour ouvrir un retour.");
        }

        var hasOpen = await _db.ReturnRequests.AsNoTracking()
            .AnyAsync(
                r => r.OrderId == order.Id &&
                     r.UserId == userId &&
                     (r.Status == StatusRequested || r.Status == StatusApproved),
                ct);
        if (hasOpen)
        {
            return BadRequest("Une demande de retour est deja en cours pour cette commande.");
        }

        var committedRows = await _db.ReturnRequests.AsNoTracking()
            .Where(r =>
                r.OrderId == order.Id &&
                r.UserId == userId &&
                (r.Status == StatusApproved || r.Status == StatusRefunded))
            .Select(r => new { r.ApprovedAmount, r.RequestedAmount })
            .ToListAsync(ct);

        var alreadyCommitted = committedRows.Sum(x => x.ApprovedAmount ?? x.RequestedAmount);

        var remaining = decimal.Round(order.TotalAmount - alreadyCommitted, 2, MidpointRounding.AwayFromZero);
        if (remaining <= 0m)
        {
            return BadRequest("Montant remboursable deja epuise pour cette commande.");
        }

        var requestedAmount = req.RequestedAmount ?? remaining;
        requestedAmount = decimal.Round(requestedAmount, 2, MidpointRounding.AwayFromZero);
        if (requestedAmount <= 0m)
        {
            return BadRequest("Le montant demande doit etre superieur a zero.");
        }

        if (requestedAmount > remaining)
        {
            return BadRequest($"Le montant demande depasse le restant remboursable ({remaining:0.00}).");
        }

        var now = DateTime.UtcNow;
        var slaHours = reasonDef?.DefaultSlaHours ?? DefaultSlaHours;
        var slaTarget = now.AddHours(Math.Clamp(slaHours, 1, 24 * 30));

        var entity = new ReturnRequest
        {
            OrderId = order.Id,
            UserId = userId,
            Status = StatusRequested,
            ReasonCode = normalizedReasonCode,
            Reason = reason,
            Comment = comment,
            RequestedAmount = requestedAmount,
            Currency = string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency.Trim().ToUpperInvariant(),
            SlaTargetAtUtc = slaTarget,
            LastStatusChangedAtUtc = now,
            CreatedAtUtc = now
        };

        _db.ReturnRequests.Add(entity);
        _db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
        {
            ReturnRequest = entity,
            OldStatus = "None",
            NewStatus = StatusRequested,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            Note = "Return request created by client."
        });
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = entity.Id },
            ToDto(entity, order.Id));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ReturnRequestDto>>> Mine(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var normalizedStatus = NormalizeStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status de retour invalide.");
        }

        var query = _db.ReturnRequests.AsNoTracking()
            .Where(r => r.UserId == userId);

        if (normalizedStatus is not null)
        {
            query = query.Where(r => r.Status == normalizedStatus);
        }

        var nowUtc = DateTime.UtcNow;
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReturnRequestDto(
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
                r.SlaTargetAtUtc.Value < nowUtc
            ))
            .ToListAsync(ct);

        return Ok(new PagedResult<ReturnRequestDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReturnRequestDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var nowUtc = DateTime.UtcNow;

        var row = await _db.ReturnRequests.AsNoTracking()
            .Where(r => r.Id == id && r.UserId == userId)
            .Select(r => new ReturnRequestDto(
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
                r.SlaTargetAtUtc.Value < nowUtc
            ))
            .FirstOrDefaultAsync(ct);

        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<ReturnTimelineEventDto>>> GetTimeline(Guid id, CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var exists = await _db.ReturnRequests.AsNoTracking()
            .AnyAsync(r => r.Id == id && r.UserId == userId, ct);
        if (!exists)
        {
            return NotFound();
        }

        var rows = await _db.ReturnRequestStatusHistories.AsNoTracking()
            .Include(x => x.ChangedByUser)
            .Where(x => x.ReturnRequestId == id)
            .OrderBy(x => x.ChangedAtUtc)
            .Select(x => new ReturnTimelineEventDto(
                x.Id,
                x.OldStatus,
                x.NewStatus,
                x.Note,
                x.ChangedAtUtc,
                x.ChangedByUser != null ? x.ChangedByUser.Email : null))
            .ToListAsync(ct);

        return Ok(rows);
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

        if (string.Equals(value, StatusCancelled, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCancelled;
        }

        return null;
    }

    private static string? NormalizeReasonCode(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return null;
        }

        return reasonCode.Trim().ToUpperInvariant();
    }

    private static ReturnRequestDto ToDto(ReturnRequest row, Guid orderId)
        => new(
            row.Id,
            orderId,
            BuildOrderCode(orderId),
            row.Status,
            row.ReasonCode,
            row.Reason,
            row.Comment,
            row.RequestedAmount,
            row.ApprovedAmount,
            row.Currency,
            row.AdminNote,
            row.CreatedAtUtc,
            row.ReviewedAtUtc,
            row.RefundedAtUtc,
            row.SlaTargetAtUtc,
            row.LastStatusChangedAtUtc,
            row.SlaTargetAtUtc.HasValue &&
            (row.Status == StatusRequested || row.Status == StatusApproved) &&
            row.SlaTargetAtUtc.Value < DateTime.UtcNow);

    private static string BuildOrderCode(Guid orderId)
        => $"DM{orderId.ToString("N")[..6].ToUpperInvariant()}";
}
