using DoorMarket.Application.DTOs.Marketing;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/marketing")]
public class MarketingController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public MarketingController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("banners")]
    public async Task<ActionResult<List<MarketingBannerDto>>> GetBanners(
        [FromQuery] string? lang,
        [FromQuery] string? city,
        [FromQuery] string? zone,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? shopId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _db.MarketingBanners.AsNoTracking()
            .Where(b => b.IsActive)
            .Where(b => !b.StartAtUtc.HasValue || b.StartAtUtc <= now)
            .Where(b => !b.EndAtUtc.HasValue || b.EndAtUtc > now);

        if (!string.IsNullOrWhiteSpace(lang))
        {
            var l = lang.Trim().ToLowerInvariant();
            query = query.Where(b => b.Language == null || b.Language.ToLower() == l);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim();
            query = query.Where(b => b.City == null || b.City == c);
        }

        if (!string.IsNullOrWhiteSpace(zone))
        {
            var z = zone.Trim();
            query = query.Where(b => b.Zone == null || b.Zone == z);
        }

        if (categoryId.HasValue)
        {
            var id = categoryId.Value;
            query = query.Where(b => b.CategoryId == null || b.CategoryId == id);
        }

        if (shopId.HasValue)
        {
            var id = shopId.Value;
            query = query.Where(b => b.ShopId == null || b.ShopId == id);
        }

        var items = await query
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpPost("banners/{id:guid}/impression")]
    public async Task<IActionResult> TrackImpression(Guid id, CancellationToken ct)
    {
        var banner = await _db.MarketingBanners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return NotFound();

        banner.Impressions += 1;
        banner.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("banners/{id:guid}/click")]
    public async Task<IActionResult> TrackClick(Guid id, CancellationToken ct)
    {
        var banner = await _db.MarketingBanners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return NotFound();

        banner.Clicks += 1;
        banner.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private MarketingBannerDto ToDto(Domain.Entities.MarketingBanner b) =>
        new(
            b.Id,
            b.Title,
            b.Subtitle,
            DoorMarket.Api.Utils.ResponseUrlNormalizer.ToAbsoluteUrl(b.ImageUrl, Request),
            b.TargetUrl,
            b.IsActive,
            b.StartAtUtc,
            b.EndAtUtc,
            b.Language,
            b.City,
            b.Zone,
            b.CategoryId,
            b.ShopId,
            b.SortOrder,
            b.Impressions,
            b.Clicks,
            b.CreatedAtUtc
        );
}
