using System.Text.Json;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/shops/{shopId:guid}/reviews")]
public class ShopReviewsController : ControllerBase
{
    private const int MaxCommentLength = 1000;
    private const int MaxPhotoUrls = 8;
    private const int MaxVideoUrls = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public ShopReviewsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ShopReviewSummaryDto>> Summary(Guid shopId, CancellationToken ct = default)
    {
        var aggregate = await _db.ShopReviews.AsNoTracking()
            .Where(r => r.ShopId == shopId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ReviewCount = g.Count(),
                AverageRating = g.Average(x => (double)x.Rating),
                Count1 = g.Count(x => x.Rating == 1),
                Count2 = g.Count(x => x.Rating == 2),
                Count3 = g.Count(x => x.Rating == 3),
                Count4 = g.Count(x => x.Rating == 4),
                Count5 = g.Count(x => x.Rating == 5)
            })
            .FirstOrDefaultAsync(ct);

        if (aggregate is null)
        {
            return Ok(new ShopReviewSummaryDto(shopId, null, 0, 0, 0, 0, 0, 0));
        }

        return Ok(new ShopReviewSummaryDto(
            shopId,
            decimal.Round((decimal)aggregate.AverageRating, 2, MidpointRounding.AwayFromZero),
            aggregate.ReviewCount,
            aggregate.Count1,
            aggregate.Count2,
            aggregate.Count3,
            aggregate.Count4,
            aggregate.Count5));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShopReviewDto>>> List(
        Guid shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var normalizedSort = NormalizeSort(sort);
        if (sort is not null && normalizedSort is null)
        {
            return BadRequest("Sort invalide. Utiliser recent, rating_desc, rating_asc ou verified_purchase.");
        }

        var query = _db.ShopReviews.AsNoTracking()
            .Where(r => r.ShopId == shopId);

        query = ApplySort(query, normalizedSort);

        var total = await query.CountAsync(ct);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.ShopId,
                r.UserId,
                r.Rating,
                r.Comment,
                r.CreatedAtUtc,
                UserEmail = r.User.Email,
                r.IsVerifiedPurchase,
                r.PhotoUrlsJson,
                r.VideoUrlsJson,
                r.HelpfulCount
            })
            .ToListAsync(ct);

        var currentUserId = _current.UserId;
        var reviewIds = rows.Select(r => r.Id).ToList();
        HashSet<Guid> helpfulByCurrentUser = new();
        if (currentUserId.HasValue && reviewIds.Count > 0)
        {
            var helpfulIds = await _db.ShopReviewHelpfulVotes.AsNoTracking()
                .Where(v => v.UserId == currentUserId.Value && v.IsHelpful && reviewIds.Contains(v.ShopReviewId))
                .Select(v => v.ShopReviewId)
                .ToListAsync(ct);
            helpfulByCurrentUser = helpfulIds.ToHashSet();
        }

        var items = rows.Select(r => new ShopReviewDto(
                r.Id,
                r.ShopId,
                r.UserId,
                r.Rating,
                r.Comment,
                r.CreatedAtUtc,
                r.UserEmail,
                r.IsVerifiedPurchase,
                DeserializeUrls(r.PhotoUrlsJson),
                DeserializeUrls(r.VideoUrlsJson),
                r.HelpfulCount,
                helpfulByCurrentUser.Contains(r.Id)))
            .ToList();

        return Ok(new PagedResult<ShopReviewDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ShopReviewDto>> Create(
        Guid shopId,
        [FromBody] CreateShopReviewRequest req,
        CancellationToken ct)
    {
        if (req.Rating is < 1 or > 5)
        {
            return BadRequest("La note doit etre entre 1 et 5.");
        }

        var normalizedComment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        if (normalizedComment is not null && normalizedComment.Length > MaxCommentLength)
        {
            return BadRequest($"Le commentaire ne doit pas depasser {MaxCommentLength} caracteres.");
        }

        var photoUrlsResult = NormalizeMediaUrls(req.PhotoUrls, MaxPhotoUrls, "photo");
        if (!photoUrlsResult.IsValid)
        {
            return BadRequest(photoUrlsResult.Error);
        }

        var videoUrlsResult = NormalizeMediaUrls(req.VideoUrls, MaxVideoUrls, "video");
        if (!videoUrlsResult.IsValid)
        {
            return BadRequest(videoUrlsResult.Error);
        }

        var exists = await _db.Shops.AsNoTracking()
            .AnyAsync(s => s.Id == shopId, ct);
        if (!exists)
        {
            return NotFound();
        }

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var verifiedPurchase = await HasVerifiedPurchaseAsync(userId, shopId, req.OrderId, ct);
        if (req.OrderId.HasValue && !verifiedPurchase)
        {
            return BadRequest("OrderId invalide pour un avis verifie.");
        }

        var photoUrlsJson = SerializeUrls(photoUrlsResult.Items);
        var videoUrlsJson = SerializeUrls(videoUrlsResult.Items);

        var existingReview = await _db.ShopReviews
            .FirstOrDefaultAsync(r => r.ShopId == shopId && r.UserId == userId, ct);

        ShopReview review;
        if (existingReview is null)
        {
            review = new ShopReview
            {
                ShopId = shopId,
                UserId = userId,
                Rating = req.Rating,
                Comment = normalizedComment,
                OrderId = req.OrderId,
                IsVerifiedPurchase = verifiedPurchase,
                PhotoUrlsJson = photoUrlsJson,
                VideoUrlsJson = videoUrlsJson,
                HelpfulCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ShopReviews.Add(review);
        }
        else
        {
            existingReview.Rating = req.Rating;
            existingReview.Comment = normalizedComment;
            existingReview.OrderId = req.OrderId;
            existingReview.IsVerifiedPurchase = verifiedPurchase;
            existingReview.PhotoUrlsJson = photoUrlsJson;
            existingReview.VideoUrlsJson = videoUrlsJson;
            existingReview.UpdatedAtUtc = DateTime.UtcNow;
            review = existingReview;
        }

        await _db.SaveChangesAsync(ct);

        var email = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        return Ok(new ShopReviewDto(
            review.Id,
            review.ShopId,
            review.UserId,
            review.Rating,
            review.Comment,
            review.CreatedAtUtc,
            email,
            review.IsVerifiedPurchase,
            photoUrlsResult.Items,
            videoUrlsResult.Items,
            review.HelpfulCount,
            false
        ));
    }

    [Authorize]
    [HttpPost("{reviewId:guid}/helpful")]
    public async Task<ActionResult<ShopReviewHelpfulVoteResultDto>> VoteHelpful(
        Guid shopId,
        Guid reviewId,
        [FromBody] VoteShopReviewHelpfulRequest? req,
        CancellationToken ct = default)
    {
        req ??= new VoteShopReviewHelpfulRequest(true);

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var review = await _db.ShopReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ShopId == shopId, ct);
        if (review is null)
        {
            return NotFound();
        }

        var vote = await _db.ShopReviewHelpfulVotes
            .FirstOrDefaultAsync(v => v.ShopReviewId == reviewId && v.UserId == userId, ct);

        if (vote is null)
        {
            if (req.IsHelpful)
            {
                _db.ShopReviewHelpfulVotes.Add(new ShopReviewHelpfulVote
                {
                    ShopReviewId = reviewId,
                    UserId = userId,
                    IsHelpful = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
                review.HelpfulCount += 1;
            }
        }
        else if (vote.IsHelpful != req.IsHelpful)
        {
            if (req.IsHelpful)
            {
                review.HelpfulCount += 1;
            }
            else if (review.HelpfulCount > 0)
            {
                review.HelpfulCount -= 1;
            }

            vote.IsHelpful = req.IsHelpful;
            vote.UpdatedAtUtc = DateTime.UtcNow;
        }

        review.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new ShopReviewHelpfulVoteResultDto(reviewId, review.HelpfulCount, req.IsHelpful));
    }

    private async Task<bool> HasVerifiedPurchaseAsync(
        Guid userId,
        Guid shopId,
        Guid? orderId,
        CancellationToken ct)
    {
        var ordersQuery = _db.Orders.AsNoTracking()
            .Where(o =>
                o.UserId == userId &&
                o.PaymentStatus == "Paid" &&
                (o.FulfillmentStatus == "Delivered" || o.Status == "Delivered" || o.Status == "Completed"));

        if (orderId.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.Id == orderId.Value);
        }

        return await (
            from o in ordersQuery
            join i in _db.OrderItems.AsNoTracking() on o.Id equals i.OrderId
            join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id
            where p.ShopId == shopId
            select o.Id
        ).AnyAsync(ct);
    }

    private static IQueryable<ShopReview> ApplySort(IQueryable<ShopReview> query, string? normalizedSort)
        => normalizedSort switch
        {
            "rating_desc" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAtUtc),
            "rating_asc" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAtUtc),
            "verified_purchase" => query.OrderByDescending(r => r.IsVerifiedPurchase).ThenByDescending(r => r.CreatedAtUtc),
            _ => query.OrderByDescending(r => r.CreatedAtUtc)
        };

    private static string? NormalizeSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return "recent";
        }

        return sort.Trim().ToLowerInvariant() switch
        {
            "recent" => "recent",
            "rating_desc" => "rating_desc",
            "rating_asc" => "rating_asc",
            "verified_purchase" => "verified_purchase",
            _ => null
        };
    }

    private static MediaUrlsValidationResult NormalizeMediaUrls(IReadOnlyList<string>? values, int maxCount, string kind)
    {
        if (values is null || values.Count == 0)
        {
            return MediaUrlsValidationResult.Valid(new List<string>());
        }

        if (values.Count > maxCount)
        {
            return MediaUrlsValidationResult.Fail($"Le nombre de {kind}s ne doit pas depasser {maxCount}.");
        }

        var normalized = new List<string>();
        foreach (var raw in values)
        {
            var value = raw?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Length > 512)
            {
                return MediaUrlsValidationResult.Fail($"Une URL de {kind} est trop longue.");
            }

            if (!IsValidMediaUrl(value))
            {
                return MediaUrlsValidationResult.Fail($"URL de {kind} invalide: {value}");
            }

            normalized.Add(value);
        }

        return MediaUrlsValidationResult.Valid(normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool IsValidMediaUrl(string value)
    {
        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static string? SerializeUrls(IReadOnlyList<string> values)
        => values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);

    private static IReadOnlyList<string> DeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return parsed is null ? Array.Empty<string>() : parsed;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private sealed record MediaUrlsValidationResult(bool IsValid, IReadOnlyList<string> Items, string? Error)
    {
        public static MediaUrlsValidationResult Valid(IReadOnlyList<string> items)
            => new(true, items, null);

        public static MediaUrlsValidationResult Fail(string error)
            => new(false, Array.Empty<string>(), error);
    }
}
