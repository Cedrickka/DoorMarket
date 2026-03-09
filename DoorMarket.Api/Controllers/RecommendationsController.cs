using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public RecommendationsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("products")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RecommendedProductDto>>> GetProducts(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 60);

        var userId = _current.UserId;
        var preferredCategories = new Dictionary<Guid, int>();
        var preferredShops = new Dictionary<Guid, int>();
        var recentViewed = new HashSet<Guid>();

        if (userId.HasValue)
        {
            var bought = await (
                from o in _db.Orders.AsNoTracking()
                where o.UserId == userId.Value && o.PaymentStatus == "Paid"
                join i in _db.OrderItems.AsNoTracking() on o.Id equals i.OrderId
                join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id
                select new { p.CategoryId, p.ShopId, i.Qty }
            ).ToListAsync(ct);

            foreach (var row in bought)
            {
                preferredCategories[row.CategoryId] = preferredCategories.TryGetValue(row.CategoryId, out var categoryCount)
                    ? categoryCount + row.Qty
                    : row.Qty;
                preferredShops[row.ShopId] = preferredShops.TryGetValue(row.ShopId, out var shopCount)
                    ? shopCount + row.Qty
                    : row.Qty;
            }

            var viewed = await _db.SearchAnalyticsEvents.AsNoTracking()
                .Where(x =>
                    x.UserId == userId.Value &&
                    x.EventType == "Click" &&
                    x.TargetType == "Product" &&
                    x.TargetId.HasValue)
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(200)
                .Select(x => x.TargetId!.Value)
                .ToListAsync(ct);
            recentViewed = viewed.ToHashSet();
        }

        var candidates = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQty > 0)
            .OrderByDescending(p => p.IsPromotionEnabled)
            .ThenByDescending(p => p.UpdatedAtUtc ?? p.CreatedAtUtc)
            .Take(400)
            .Select(p => new
            {
                p.Id,
                p.ShopId,
                p.CategoryId,
                p.Name,
                p.Price,
                p.PromotionPrice,
                p.IsPromotionEnabled,
                p.PromotionStartUtc,
                p.PromotionEndUtc,
                p.Currency,
                p.StockQty,
                p.MainImageUrl,
                p.CreatedAtUtc,
                ShopName = p.Shop.Name,
                CategoryName = p.Category.Name,
                ShopRating = _db.ShopReviews
                    .Where(r => r.ShopId == p.ShopId)
                    .Select(r => (double?)r.Rating)
                    .Average(),
                ShopReviewCount = _db.ShopReviews.Count(r => r.ShopId == p.ShopId)
            })
            .ToListAsync(ct);

        var nowUtc = DateTime.UtcNow;
        var rows = candidates
            .Select(c =>
            {
                var promoActive =
                    c.IsPromotionEnabled &&
                    c.PromotionPrice.HasValue &&
                    (!c.PromotionStartUtc.HasValue || c.PromotionStartUtc <= nowUtc) &&
                    (!c.PromotionEndUtc.HasValue || c.PromotionEndUtc > nowUtc);
                var effectivePrice = promoActive ? c.PromotionPrice!.Value : c.Price;

                var score = 0m;
                var reasons = new List<string>();

                if (preferredCategories.TryGetValue(c.CategoryId, out var categoryWeight))
                {
                    score += 40m + Math.Min(categoryWeight, 10) * 3m;
                    reasons.Add("category_affinity");
                }

                if (preferredShops.TryGetValue(c.ShopId, out var shopWeight))
                {
                    score += 30m + Math.Min(shopWeight, 10) * 2m;
                    reasons.Add("shop_affinity");
                }

                if (recentViewed.Contains(c.Id))
                {
                    score += 25m;
                    reasons.Add("recently_viewed");
                }

                if (promoActive)
                {
                    score += 10m;
                    reasons.Add("promotion");
                }

                score += Math.Min(c.StockQty, 100) / 10m;
                score += (decimal)(c.ShopRating ?? 0d) * 2m;
                score += c.ShopReviewCount > 0 ? Math.Min(c.ShopReviewCount, 100) / 20m : 0m;

                if (reasons.Count == 0)
                {
                    reasons.Add("popular");
                }

                return new RecommendedProductDto(
                    c.Id,
                    c.Name,
                    c.ShopId,
                    c.ShopName,
                    c.CategoryId,
                    c.CategoryName,
                    c.Price,
                    effectivePrice,
                    promoActive,
                    c.Currency,
                    c.StockQty,
                    c.MainImageUrl,
                    c.ShopRating,
                    c.ShopReviewCount,
                    decimal.Round(score, 2, MidpointRounding.AwayFromZero),
                    reasons);
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.ShopReviewCount)
            .ThenBy(x => x.EffectivePrice)
            .Take(limit)
            .ToList();

        return Ok(rows);
    }

    public sealed record RecommendedProductDto(
        Guid Id,
        string Name,
        Guid ShopId,
        string ShopName,
        Guid CategoryId,
        string CategoryName,
        decimal Price,
        decimal EffectivePrice,
        bool HasActivePromotion,
        string Currency,
        int StockQty,
        string? MainImageUrl,
        double? ShopRating,
        int ShopReviewCount,
        decimal Score,
        IReadOnlyList<string> Reasons);
}
