using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/conversion")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminConversionController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminConversionController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("funnel")]
    public async Task<ActionResult<ConversionFunnelDto>> GetFunnel(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);

        var searchEvents = _db.SearchAnalyticsEvents.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc);

        var visitorUsers = await searchEvents
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .CountAsync(ct);
        var visitorSessions = await searchEvents
            .Where(x => !string.IsNullOrWhiteSpace(x.SessionId))
            .Select(x => x.SessionId!)
            .Distinct()
            .CountAsync(ct);
        var visitors = Math.Max(visitorUsers, visitorSessions);

        var searches = await searchEvents.CountAsync(x => x.EventType == "Query", ct);
        var productClicks = await searchEvents.CountAsync(x => x.EventType == "Click" && x.TargetType == "Product", ct);

        var cartUsers = await _db.CartItems.AsNoTracking()
            .Where(x => (x.UpdatedAtUtc ?? x.CreatedAtUtc) >= fromUtc && (x.UpdatedAtUtc ?? x.CreatedAtUtc) < toUtc)
            .Select(x => x.Cart.UserId)
            .Distinct()
            .CountAsync(ct);

        var checkouts = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc, ct);

        var paidOrders = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc && x.PaymentStatus == "Paid", ct);

        var visitorToSearch = visitors > 0 ? RoundPct(searches, visitors) : 0m;
        var searchToClick = searches > 0 ? RoundPct(productClicks, searches) : 0m;
        var clickToCart = productClicks > 0 ? RoundPct(cartUsers, productClicks) : 0m;
        var cartToCheckout = cartUsers > 0 ? RoundPct(checkouts, cartUsers) : 0m;
        var checkoutToPaid = checkouts > 0 ? RoundPct(paidOrders, checkouts) : 0m;

        return Ok(new ConversionFunnelDto(
            fromUtc,
            toUtc,
            visitors,
            searches,
            productClicks,
            cartUsers,
            checkouts,
            paidOrders,
            visitorToSearch,
            searchToClick,
            clickToCart,
            cartToCheckout,
            checkoutToPaid));
    }

    [HttpGet("cohorts")]
    public async Task<ActionResult<IReadOnlyList<CohortRowDto>>> GetCohorts(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, 180);

        var paidOrders = await _db.Orders.AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid")
            .Select(x => new { x.UserId, x.CreatedAtUtc })
            .ToListAsync(ct);

        var inWindowOrders = paidOrders
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
            .ToList();

        var firstOrderByUser = paidOrders
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.CreatedAtUtc));

        var cohortRows = inWindowOrders
            .GroupBy(x =>
            {
                var first = firstOrderByUser[x.UserId];
                return new DateTime(first.Year, first.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            })
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var users = g.Select(x => x.UserId).Distinct().ToList();
                var cohortSize = users.Count;
                var repeatWithin30d = users.Count(userId =>
                {
                    var first = firstOrderByUser[userId];
                    var limit = first.AddDays(30);
                    return paidOrders.Count(x => x.UserId == userId && x.CreatedAtUtc > first && x.CreatedAtUtc <= limit) > 0;
                });
                var repeatRate = cohortSize > 0 ? decimal.Round((decimal)repeatWithin30d * 100m / cohortSize, 2, MidpointRounding.AwayFromZero) : 0m;
                return new CohortRowDto(g.Key, cohortSize, repeatWithin30d, repeatRate);
            })
            .ToList();

        return Ok(cohortRows);
    }

    [HttpGet("campaign-roi")]
    public async Task<ActionResult<IReadOnlyList<CampaignRoiRowDto>>> GetCampaignRoi(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, 90);

        var rows = await _db.MarketingCampaignRuns.AsNoTracking()
            .Include(x => x.Campaign)
            .Where(x => x.StartedAtUtc >= fromUtc && x.StartedAtUtc < toUtc)
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.CampaignId,
                CampaignName = x.Campaign.Name,
                x.TargetUsers,
                x.SentCount,
                x.FailedCount,
                x.RevenueAttributed,
                x.DiscountCost,
                x.StartedAtUtc
            })
            .ToListAsync(ct);

        var result = rows.Select(x =>
        {
            var roiPercent = x.DiscountCost > 0m
                ? decimal.Round((x.RevenueAttributed - x.DiscountCost) * 100m / x.DiscountCost, 2, MidpointRounding.AwayFromZero)
                : 0m;
            return new CampaignRoiRowDto(
                x.Id,
                x.CampaignId,
                x.CampaignName,
                x.TargetUsers,
                x.SentCount,
                x.FailedCount,
                x.RevenueAttributed,
                x.DiscountCost,
                roiPercent,
                x.StartedAtUtc);
        }).ToList();

        return Ok(result);
    }

    [HttpGet("ab/cart-recovery")]
    public async Task<ActionResult<IReadOnlyList<CartRecoveryAbRowDto>>> GetCartRecoveryAb(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to, 60);
        var events = await _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.DetectedAtUtc >= fromUtc && x.DetectedAtUtc < toUtc)
            .Select(x => new
            {
                x.UserId,
                x.ExperimentGroup,
                x.ReminderStatus,
                x.DetectedAtUtc
            })
            .ToListAsync(ct);

        var paidOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == "Paid" && o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc < toUtc.AddDays(7))
            .Select(o => new { o.UserId, o.CreatedAtUtc })
            .ToListAsync(ct);

        var rows = events
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ExperimentGroup) ? "A" : x.ExperimentGroup)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var detected = g.Count();
                var remindersSent = g.Count(x => x.ReminderStatus == "Sent" || x.ReminderStatus == "Partial");
                var resumed = g.Count(x =>
                    paidOrders.Any(o =>
                        o.UserId == x.UserId &&
                        o.CreatedAtUtc >= x.DetectedAtUtc &&
                        o.CreatedAtUtc <= x.DetectedAtUtc.AddDays(7)));
                var conversionRate = detected > 0
                    ? decimal.Round((decimal)resumed * 100m / detected, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new CartRecoveryAbRowDto(g.Key, detected, remindersSent, resumed, conversionRate);
            })
            .ToList();

        return Ok(rows);
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRange(DateTime? from, DateTime? to, int defaultDays)
    {
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddDays(-defaultDays)).ToUniversalTime();
        if (fromUtc >= toUtc)
        {
            fromUtc = toUtc.AddDays(-defaultDays);
        }

        return (fromUtc, toUtc);
    }

    private static decimal RoundPct(int numerator, int denominator)
        => denominator <= 0
            ? 0m
            : decimal.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    public sealed record ConversionFunnelDto(
        DateTime FromUtc,
        DateTime ToUtc,
        int Visitors,
        int Searches,
        int ProductClicks,
        int CartUsers,
        int Checkouts,
        int PaidOrders,
        decimal VisitorToSearchRate,
        decimal SearchToClickRate,
        decimal ClickToCartRate,
        decimal CartToCheckoutRate,
        decimal CheckoutToPaidRate);

    public sealed record CohortRowDto(
        DateTime CohortMonthUtc,
        int CohortSize,
        int RepeatWithin30Days,
        decimal RepeatWithin30DaysRate);

    public sealed record CampaignRoiRowDto(
        Guid RunId,
        Guid CampaignId,
        string CampaignName,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        DateTime StartedAtUtc);

    public sealed record CartRecoveryAbRowDto(
        string ExperimentGroup,
        int Detected,
        int RemindersSent,
        int ResumedCheckoutOrPaid,
        decimal ConversionRatePercent);
}
