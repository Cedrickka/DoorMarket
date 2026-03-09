using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/reconciliation")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminReconciliationController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly FinanceCalculator _finance;
    private readonly ICurrentUserService _current;
    private readonly IReconciliationCronService _reconciliationCron;

    public AdminReconciliationController(
        DoorMarketDbContext db,
        FinanceCalculator finance,
        ICurrentUserService current,
        IReconciliationCronService reconciliationCron)
    {
        _db = db;
        _finance = finance;
        _current = current;
        _reconciliationCron = reconciliationCron;
    }

    [HttpGet("shops")]
    public async Task<ActionResult<AdminReconciliationResponse>> GetShopReconciliation(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var items = await _finance.GetShopReconciliationAsync(from, to, ct);

        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var payoutsQuery = _db.ShopPayouts.AsNoTracking();
        if (fromUtc.HasValue)
        {
            payoutsQuery = payoutsQuery.Where(p => p.PaidOutAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            payoutsQuery = payoutsQuery.Where(p => p.PaidOutAtUtc < toUtc.Value);
        }

        payoutsQuery = payoutsQuery.Where(p => p.Status == PayoutStatusPaid);
        var totalPaidOut = await payoutsQuery.SumAsync(p => (decimal?)p.AmountPaid, ct) ?? 0m;

        return Ok(new AdminReconciliationResponse(items, totalPaidOut));
    }

    [HttpGet("payouts")]
    public async Task<ActionResult<IReadOnlyList<ShopPayoutDto>>> GetPayouts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? shopId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var query = _db.ShopPayouts.AsNoTracking()
            .Include(x => x.Shop)
            .AsQueryable();

        if (shopId.HasValue && shopId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ShopId == shopId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryNormalizePayoutStatus(status, out var normalizedStatus))
            {
                return BadRequest("Statut payout invalide.");
            }

            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc < toUtc.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x, x.Shop.Name))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("payouts/summary")]
    public async Task<ActionResult<IReadOnlyList<PayoutCurrencySummaryDto>>> GetPayoutSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? shopId,
        CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var query = _db.ShopPayouts.AsNoTracking().AsQueryable();

        if (shopId.HasValue && shopId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ShopId == shopId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < toUtc.Value);
        }

        var aggregates = await query
            .GroupBy(x => x.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                TotalCount = g.Count(),
                DraftCount = g.Sum(x => x.Status == PayoutStatusDraft ? 1 : 0),
                ApprovedCount = g.Sum(x => x.Status == PayoutStatusApproved ? 1 : 0),
                PaidCount = g.Sum(x => x.Status == PayoutStatusPaid ? 1 : 0),
                ReversedCount = g.Sum(x => x.Status == PayoutStatusReversed ? 1 : 0),
                TotalNetToPay = g.Sum(x => (decimal?)x.NetToPay) ?? 0m,
                TotalAmountPaid = g.Sum(x => x.Status == PayoutStatusPaid ? (decimal?)x.AmountPaid : 0m) ?? 0m
            })
            .OrderBy(x => x.Currency)
            .ToListAsync(ct);

        var rows = aggregates
            .Select(x => new PayoutCurrencySummaryDto(
                x.Currency,
                x.TotalCount,
                x.DraftCount,
                x.ApprovedCount,
                x.PaidCount,
                x.ReversedCount,
                x.TotalNetToPay,
                x.TotalAmountPaid))
            .ToList();

        return Ok(rows);
    }

    [HttpGet("assistant")]
    public async Task<ActionResult<ReconciliationAssistantDto>> GetAssistantInsights(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? shopId,
        [FromQuery] int draftSlaDays = 7,
        [FromQuery] int approvedSlaDays = 3,
        CancellationToken ct = default)
    {
        draftSlaDays = Math.Clamp(draftSlaDays, 1, 90);
        approvedSlaDays = Math.Clamp(approvedSlaDays, 1, 90);

        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var query = _db.ShopPayouts.AsNoTracking().AsQueryable();

        if (shopId.HasValue && shopId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ShopId == shopId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < toUtc.Value);
        }

        var payouts = await query
            .Select(x => new
            {
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
                x.PeriodEndUtc
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
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

        var partialGapTotal = partialPaid.Sum(x => x.NetToPay - x.AmountPaid);

        var insights = new List<ReconciliationInsightDto>();
        if (staleDrafts.Count > 0)
        {
            insights.Add(new ReconciliationInsightDto(
                "STALE_DRAFT_PAYOUTS",
                "High",
                $"{staleDrafts.Count} draft payout(s) older than {draftSlaDays} day(s).",
                "Review and approve drafts, or reverse if invalid to avoid payout backlog.",
                staleDrafts.Count,
                staleDrafts.Take(10).Select(x => x.Id).ToList()));
        }

        if (staleApproved.Count > 0)
        {
            insights.Add(new ReconciliationInsightDto(
                "APPROVED_NOT_PAID_SLA",
                "High",
                $"{staleApproved.Count} approved payout(s) are pending payment beyond {approvedSlaDays} day(s).",
                "Execute payment and mark paid with reference and payout date.",
                staleApproved.Count,
                staleApproved.Take(10).Select(x => x.Id).ToList()));
        }

        if (partialPaid.Count > 0)
        {
            insights.Add(new ReconciliationInsightDto(
                "PARTIAL_PAID_DELTA",
                "Medium",
                $"{partialPaid.Count} paid payout(s) are below net-to-pay. Outstanding delta: {partialGapTotal:0.##}.",
                "Confirm if partial payment is expected, then settle residual or reverse/redo payout.",
                partialPaid.Count,
                partialPaid.Take(10).Select(x => x.Id).ToList()));
        }

        if (paidWithoutReference.Count > 0)
        {
            insights.Add(new ReconciliationInsightDto(
                "PAID_WITHOUT_REFERENCE",
                "Medium",
                $"{paidWithoutReference.Count} paid payout(s) have no external payment reference.",
                "Update payout references to improve audit traceability.",
                paidWithoutReference.Count,
                paidWithoutReference.Take(10).Select(x => x.Id).ToList()));
        }

        if (overlapPairs.Count > 0)
        {
            var impactedIds = overlapPairs
                .SelectMany(x => new[] { x.First, x.Second })
                .Distinct()
                .Take(10)
                .ToList();
            insights.Add(new ReconciliationInsightDto(
                "ACTIVE_PERIOD_OVERLAP",
                "Critical",
                $"{overlapPairs.Count} overlapping active payout period pair(s) detected.",
                "Reverse duplicates and recreate a clean non-overlapping payout timeline.",
                overlapPairs.Count,
                impactedIds));
        }

        if (reversalRate >= 15m && closedCount >= 5)
        {
            insights.Add(new ReconciliationInsightDto(
                "HIGH_REVERSAL_RATE",
                "Medium",
                $"Reversal rate is high ({reversalRate:0.##}%).",
                "Audit payout preparation steps and enforce approval checklist before mark-paid.",
                reversedCount,
                new List<Guid>()));
        }

        var summary = new ReconciliationAssistantSummaryDto(
            payouts.Count,
            staleDrafts.Count,
            staleApproved.Count,
            partialPaid.Count,
            paidWithoutReference.Count,
            overlapPairs.Count,
            reversedCount,
            reversalRate,
            partialGapTotal);

        return Ok(new ReconciliationAssistantDto(
            fromUtc,
            toUtc,
            draftSlaDays,
            approvedSlaDays,
            summary,
            insights.OrderByDescending(x => SeverityScore(x.Severity)).ToList()));
    }

    [HttpPost("cron/run-now")]
    public async Task<ActionResult<ReconciliationCronRunResultDto>> RunReconciliationCronNow(CancellationToken ct = default)
    {
        var result = await _reconciliationCron.RunOnceAsync("Manual", _current.UserId, ct);
        return Ok(result);
    }

    [HttpGet("cron/runs")]
    public async Task<ActionResult<ReconciliationCronRunsPageDto>> GetReconciliationCronRuns(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var runs = await _reconciliationCron.GetRunsAsync(fromUtc, toUtc, page, pageSize, ct);
        return Ok(runs);
    }

    [HttpGet("cron/summary")]
    public async Task<ActionResult<ReconciliationCronSummaryDto>> GetReconciliationCronSummary(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var summary = await _reconciliationCron.GetSummaryAsync(fromUtc, toUtc, ct);
        return Ok(summary);
    }

    [HttpPost("payouts")]
    public async Task<ActionResult<ShopPayoutDto>> CreatePayout([FromBody] CreateShopPayoutRequest req, CancellationToken ct)
    {
        if (req.ShopId == Guid.Empty)
            return BadRequest("ShopId requis.");

        if (req.AmountPaid <= 0m)
            return BadRequest("AmountPaid doit etre superieur a 0.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.ShopId, ct);
        if (shop is null) return NotFound("Boutique introuvable.");

        var periodStartUtc = req.PeriodStartUtc.ToUniversalTime();
        var periodEndUtc = req.PeriodEndUtc.ToUniversalTime();
        if (periodEndUtc <= periodStartUtc)
        {
            return BadRequest("Periode invalide: la date de fin doit etre apres la date de debut.");
        }

        var currency = NormalizeCurrency(req.Currency);
        if (currency is null)
        {
            return BadRequest("Currency invalide.");
        }

        var idempotencyKey = NormalizeIdempotencyKey(req.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existingByKey = await _db.ShopPayouts
                .AsNoTracking()
                .Include(x => x.Shop)
                .FirstOrDefaultAsync(x => x.ShopId == req.ShopId && x.IdempotencyKey == idempotencyKey, ct);
            if (existingByKey is not null)
            {
                return Ok(ToDto(existingByKey, existingByKey.Shop.Name));
            }
        }

        var summary = await _finance.GetShopTotalsAsync(req.ShopId, currency, periodStartUtc, periodEndUtc, ct);
        if (summary is null)
        {
            return BadRequest("Aucune vente payee sur la periode et la devise selectionnee.");
        }

        if (req.AmountPaid > summary.NetToPay)
        {
            return BadRequest("AmountPaid ne peut pas depasser NetToPay.");
        }

        var duplicateActive = await _db.ShopPayouts.AsNoTracking()
            .AnyAsync(x =>
                x.ShopId == req.ShopId &&
                x.Currency == currency &&
                x.PeriodStartUtc < periodEndUtc &&
                x.PeriodEndUtc > periodStartUtc &&
                x.Status != PayoutStatusReversed, ct);
        if (duplicateActive)
        {
            return Conflict("Un payout actif chevauche deja cette periode pour la boutique/devise.");
        }

        var payout = new Domain.Entities.ShopPayout
        {
            ShopId = req.ShopId,
            Currency = currency,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            GrossSalesItems = summary.GrossSalesItems,
            PlatformFee = summary.PlatformFee,
            NetToPay = summary.NetToPay,
            DeliveryRevenue = 0m,
            AmountPaid = req.AmountPaid,
            Status = PayoutStatusDraft,
            IdempotencyKey = idempotencyKey,
            PaidOutAtUtc = null,
            Reference = NormalizeReference(req.Reference)
        };

        _db.ShopPayoutStatusHistories.Add(new Domain.Entities.ShopPayoutStatusHistory
        {
            ShopPayout = payout,
            OldStatus = "None",
            NewStatus = PayoutStatusDraft,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = DateTime.UtcNow,
            Note = "Payout draft created.",
            IdempotencyKey = idempotencyKey
        });

        _db.ShopPayouts.Add(payout);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(payout, shop.Name));
    }

    [HttpPost("payouts/{id:guid}/approve")]
    public async Task<ActionResult<ShopPayoutDto>> ApprovePayout(Guid id, [FromBody] PayoutTransitionRequest req, CancellationToken ct)
    {
        var payout = await _db.ShopPayouts.Include(x => x.Shop).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (payout is null) return NotFound("Payout introuvable.");

        var idempotencyKey = NormalizeIdempotencyKey(req.IdempotencyKey);
        var note = NormalizeNote(req.Note);
        if (await IsDuplicateTransitionAsync(payout.Id, PayoutStatusApproved, idempotencyKey, ct))
        {
            return Ok(ToDto(payout, payout.Shop.Name));
        }

        if (!string.Equals(payout.Status, PayoutStatusDraft, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict($"Transition invalide: {payout.Status} -> {PayoutStatusApproved}.");
        }

        ChangePayoutStatus(payout, PayoutStatusApproved, note ?? "Payout approved.", idempotencyKey);
        payout.ApprovedAtUtc = DateTime.UtcNow;
        payout.ApprovedByUserId = _current.UserId;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(payout, payout.Shop.Name));
    }

    [HttpPost("payouts/{id:guid}/mark-paid")]
    public async Task<ActionResult<ShopPayoutDto>> MarkPayoutAsPaid(Guid id, [FromBody] MarkPayoutPaidRequest req, CancellationToken ct)
    {
        var payout = await _db.ShopPayouts.Include(x => x.Shop).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (payout is null) return NotFound("Payout introuvable.");

        var idempotencyKey = NormalizeIdempotencyKey(req.IdempotencyKey);
        if (await IsDuplicateTransitionAsync(payout.Id, PayoutStatusPaid, idempotencyKey, ct))
        {
            return Ok(ToDto(payout, payout.Shop.Name));
        }

        if (!string.Equals(payout.Status, PayoutStatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict($"Transition invalide: {payout.Status} -> {PayoutStatusPaid}.");
        }

        if (req.AmountPaid.HasValue)
        {
            if (req.AmountPaid.Value <= 0m)
            {
                return BadRequest("AmountPaid doit etre superieur a 0.");
            }

            if (req.AmountPaid.Value > payout.NetToPay)
            {
                return BadRequest("AmountPaid ne peut pas depasser NetToPay.");
            }

            payout.AmountPaid = req.AmountPaid.Value;
        }

        var paidOutAtUtc = req.PaidOutAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (paidOutAtUtc < payout.PeriodStartUtc)
        {
            return BadRequest("PaidOutAtUtc ne peut pas etre avant le debut de periode.");
        }

        if (payout.ApprovedAtUtc.HasValue && paidOutAtUtc < payout.ApprovedAtUtc.Value)
        {
            return BadRequest("PaidOutAtUtc ne peut pas etre avant l'approbation.");
        }

        payout.PaidOutAtUtc = paidOutAtUtc;
        payout.PaidByUserId = _current.UserId;
        payout.Reference = NormalizeReference(req.Reference) ?? payout.Reference;

        ChangePayoutStatus(
            payout,
            PayoutStatusPaid,
            NormalizeNote(req.Note) ?? "Payout marked as paid.",
            idempotencyKey);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(payout, payout.Shop.Name));
    }

    [HttpPost("payouts/{id:guid}/reverse")]
    public async Task<ActionResult<ShopPayoutDto>> ReversePayout(Guid id, [FromBody] ReversePayoutRequest req, CancellationToken ct)
    {
        var payout = await _db.ShopPayouts.Include(x => x.Shop).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (payout is null) return NotFound("Payout introuvable.");

        var idempotencyKey = NormalizeIdempotencyKey(req.IdempotencyKey);
        if (await IsDuplicateTransitionAsync(payout.Id, PayoutStatusReversed, idempotencyKey, ct))
        {
            return Ok(ToDto(payout, payout.Shop.Name));
        }

        if (!string.Equals(payout.Status, PayoutStatusPaid, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict($"Transition invalide: {payout.Status} -> {PayoutStatusReversed}.");
        }

        var reason = NormalizeNote(req.Reason);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest("Reason requis pour reverse.");
        }

        payout.ReversedAtUtc = DateTime.UtcNow;
        payout.ReversedByUserId = _current.UserId;
        payout.ReversalReason = reason;

        ChangePayoutStatus(payout, PayoutStatusReversed, reason, idempotencyKey);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(payout, payout.Shop.Name));
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) NormalizeRange(DateTime? from, DateTime? to)
    {
        DateTime? fromUtc = from?.ToUniversalTime();
        DateTime? toUtc = to?.ToUniversalTime();

        if (toUtc.HasValue && toUtc.Value.TimeOfDay == TimeSpan.Zero)
        {
            toUtc = toUtc.Value.AddDays(1);
        }

        return (fromUtc, toUtc);
    }

    private void ChangePayoutStatus(
        Domain.Entities.ShopPayout payout,
        string targetStatus,
        string note,
        string? idempotencyKey)
    {
        var previous = payout.Status;
        payout.Status = targetStatus;
        payout.UpdatedAtUtc = DateTime.UtcNow;

        _db.ShopPayoutStatusHistories.Add(new Domain.Entities.ShopPayoutStatusHistory
        {
            ShopPayoutId = payout.Id,
            OldStatus = previous,
            NewStatus = targetStatus,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = DateTime.UtcNow,
            Note = note,
            IdempotencyKey = idempotencyKey
        });
    }

    private async Task<bool> IsDuplicateTransitionAsync(Guid payoutId, string targetStatus, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        return await _db.ShopPayoutStatusHistories.AsNoTracking()
            .AnyAsync(x =>
                x.ShopPayoutId == payoutId &&
                x.NewStatus == targetStatus &&
                x.IdempotencyKey == idempotencyKey, ct);
    }

    private static string? NormalizeCurrency(string? rawCurrency)
    {
        var currency = (rawCurrency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3)
        {
            return null;
        }

        return currency;
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        var key = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return key.Length <= 100 ? key : key[..100];
    }

    private static string? NormalizeReference(string? value)
    {
        var reference = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return reference.Length <= 200 ? reference : reference[..200];
    }

    private static string? NormalizeNote(string? value)
    {
        var note = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        return note.Length <= 500 ? note : note[..500];
    }

    private static bool TryNormalizePayoutStatus(string? status, out string normalizedStatus)
    {
        switch ((status ?? string.Empty).Trim())
        {
            case PayoutStatusDraft:
                normalizedStatus = PayoutStatusDraft;
                return true;
            case PayoutStatusApproved:
                normalizedStatus = PayoutStatusApproved;
                return true;
            case PayoutStatusPaid:
                normalizedStatus = PayoutStatusPaid;
                return true;
            case PayoutStatusReversed:
                normalizedStatus = PayoutStatusReversed;
                return true;
            default:
                normalizedStatus = string.Empty;
                return false;
        }
    }

    private static int SeverityScore(string? severity)
        => severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            "Low" => 1,
            _ => 0
        };

    private static ShopPayoutDto ToDto(Domain.Entities.ShopPayout payout, string shopName)
        => new(
            payout.Id,
            payout.ShopId,
            shopName,
            payout.Currency,
            payout.Status,
            payout.PeriodStartUtc,
            payout.PeriodEndUtc,
            payout.GrossSalesItems,
            payout.PlatformFee,
            payout.NetToPay,
            payout.AmountPaid,
            payout.PaidOutAtUtc,
            payout.Reference,
            payout.CreatedAtUtc,
            payout.ApprovedAtUtc,
            payout.ReversedAtUtc,
            payout.ReversalReason);

    private const string PayoutStatusDraft = "Draft";
    private const string PayoutStatusApproved = "Approved";
    private const string PayoutStatusPaid = "Paid";
    private const string PayoutStatusReversed = "Reversed";

    public sealed record CreateShopPayoutRequest(
        Guid ShopId,
        string Currency,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        decimal AmountPaid,
        string? Reference,
        string? IdempotencyKey);

    public sealed record PayoutTransitionRequest(string? Note, string? IdempotencyKey);
    public sealed record MarkPayoutPaidRequest(decimal? AmountPaid, DateTime? PaidOutAtUtc, string? Reference, string? Note, string? IdempotencyKey);
    public sealed record ReversePayoutRequest(string? Reason, string? IdempotencyKey);

    public sealed record ShopReconciliationRow(
        Guid ShopId,
        string Name,
        string Currency,
        int OrdersCount,
        decimal GrossSalesItems,
        decimal PlatformFee,
        decimal NetToPay);

    public sealed record AdminReconciliationResponse(
        IReadOnlyList<FinanceCalculator.ShopReconciliationRow> Items,
        decimal TotalPaidOut);

    public sealed record ShopPayoutDto(
        Guid Id,
        Guid ShopId,
        string ShopName,
        string Currency,
        string Status,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        decimal GrossSalesItems,
        decimal PlatformFee,
        decimal NetToPay,
        decimal AmountPaid,
        DateTime? PaidOutAtUtc,
        string? Reference,
        DateTime CreatedAtUtc,
        DateTime? ApprovedAtUtc,
        DateTime? ReversedAtUtc,
        string? ReversalReason);

    public sealed record PayoutCurrencySummaryDto(
        string Currency,
        int TotalCount,
        int DraftCount,
        int ApprovedCount,
        int PaidCount,
        int ReversedCount,
        decimal TotalNetToPay,
        decimal TotalPaidOut);

    public sealed record ReconciliationAssistantDto(
        DateTime? FromUtc,
        DateTime? ToUtc,
        int DraftSlaDays,
        int ApprovedSlaDays,
        ReconciliationAssistantSummaryDto Summary,
        IReadOnlyList<ReconciliationInsightDto> Insights);

    public sealed record ReconciliationAssistantSummaryDto(
        int TotalPayouts,
        int StaleDraftCount,
        int StaleApprovedCount,
        int PartialPaidCount,
        int PaidWithoutReferenceCount,
        int OverlapPairCount,
        int ReversedCount,
        decimal ReversalRatePercent,
        decimal PartialGapTotal);

    public sealed record ReconciliationInsightDto(
        string Code,
        string Severity,
        string Detail,
        string RecommendedAction,
        int AffectedCount,
        IReadOnlyList<Guid> PayoutIds);
}
