using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Marketing;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/marketing")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminMarketingController : ControllerBase
{
    private const string BannerStatusLive = "Live";
    private const string BannerStatusPlanned = "Planned";
    private const string BannerStatusExpired = "Expired";
    private const string BannerStatusInactive = "Inactive";
    private const string CampaignStatusDraft = "Draft";
    private const string CampaignStatusActive = "Active";
    private const string CampaignStatusPaused = "Paused";
    private const string CampaignStatusCompleted = "Completed";

    private readonly DoorMarketDbContext _db;

    public AdminMarketingController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("banners")]
    public async Task<ActionResult<PagedResult<MarketingBannerDto>>> GetBanners(
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.MarketingBanners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(b => b.Title.Contains(s) || (b.Subtitle != null && b.Subtitle.Contains(s)));
        }

        if (active.HasValue)
        {
            query = query.Where(b => b.IsActive == active.Value);
        }

        query = ApplyStatusFilter(query, status, DateTime.UtcNow);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(ToDto).ToList();

        return Ok(new PagedResult<MarketingBannerDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("banners/summary")]
    public async Task<ActionResult<MarketingSummaryDto>> GetSummary(
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.MarketingBanners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(b => b.Title.Contains(s) || (b.Subtitle != null && b.Subtitle.Contains(s)));
        }

        if (active.HasValue)
        {
            query = query.Where(b => b.IsActive == active.Value);
        }

        query = ApplyStatusFilter(query, status, now);

        var rows = await query
            .Select(b => new
            {
                b.IsActive,
                b.StartAtUtc,
                b.EndAtUtc,
                b.Impressions,
                b.Clicks
            })
            .ToListAsync(ct);

        var total = rows.Count;
        var live = 0;
        var planned = 0;
        var expired = 0;
        var inactive = 0;
        long impressions = 0;
        long clicks = 0;

        foreach (var row in rows)
        {
            switch (ResolveStatus(row.IsActive, row.StartAtUtc, row.EndAtUtc, now))
            {
                case BannerStatusLive:
                    live++;
                    break;
                case BannerStatusPlanned:
                    planned++;
                    break;
                case BannerStatusExpired:
                    expired++;
                    break;
                default:
                    inactive++;
                    break;
            }

            impressions += row.Impressions;
            clicks += row.Clicks;
        }

        var ctrPercent = impressions > 0
            ? decimal.Round((decimal)clicks * 100m / impressions, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new MarketingSummaryDto(
            total,
            live,
            planned,
            expired,
            inactive,
            (int)impressions,
            (int)clicks,
            ctrPercent));
    }

    [HttpPost("automation/run")]
    public async Task<ActionResult<MarketingAutomationResultDto>> RunAutomation(
        [FromBody] RunMarketingAutomationRequest? req,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        req ??= new RunMarketingAutomationRequest(null, null, null);

        var disableExpired = req.DisableExpired ?? true;
        var autoPrioritizeLiveByCtr = req.AutoPrioritizeLiveByCtr ?? false;
        var minImpressionsForCtr = Math.Clamp(req.MinImpressionsForCtr ?? 100, 0, 100_000);

        var disabledExpired = 0;
        var reprioritizedLive = 0;

        if (disableExpired)
        {
            var expiredActive = await _db.MarketingBanners
                .Where(b => b.IsActive && b.EndAtUtc.HasValue && b.EndAtUtc.Value <= now)
                .ToListAsync(ct);

            foreach (var banner in expiredActive)
            {
                banner.IsActive = false;
                banner.UpdatedAtUtc = now;
            }

            disabledExpired = expiredActive.Count;
        }

        if (autoPrioritizeLiveByCtr)
        {
            var liveBanners = await _db.MarketingBanners
                .Where(b =>
                    b.IsActive &&
                    (!b.StartAtUtc.HasValue || b.StartAtUtc <= now) &&
                    (!b.EndAtUtc.HasValue || b.EndAtUtc > now))
                .OrderBy(b => b.SortOrder)
                .ThenByDescending(b => b.CreatedAtUtc)
                .ToListAsync(ct);

            var ranked = liveBanners
                .OrderByDescending(b => b.Impressions >= minImpressionsForCtr ? ComputeCtr(b.Impressions, b.Clicks) : decimal.MinValue)
                .ThenByDescending(b => b.Impressions)
                .ThenByDescending(b => b.Clicks)
                .ThenBy(b => b.SortOrder)
                .ThenByDescending(b => b.CreatedAtUtc)
                .ToList();

            var changed = 0;
            for (var i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].SortOrder == i)
                {
                    continue;
                }

                ranked[i].SortOrder = i;
                ranked[i].UpdatedAtUtc = now;
                changed++;
            }

            reprioritizedLive = changed;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new MarketingAutomationResultDto(
            disabledExpired,
            reprioritizedLive,
            minImpressionsForCtr,
            now));
    }

    [HttpGet("banners/{id:guid}")]
    public async Task<ActionResult<MarketingBannerDto>> GetBanner(Guid id, CancellationToken ct)
    {
        var banner = await _db.MarketingBanners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        return banner is null ? NotFound() : Ok(ToDto(banner));
    }

    [HttpPost("banners")]
    public async Task<ActionResult<MarketingBannerDto>> Create([FromBody] CreateMarketingBannerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Titre requis.");
        if (!IsValidPeriod(req.StartAtUtc, req.EndAtUtc))
            return BadRequest("La date de debut doit etre <= a la date de fin.");
        if (!IsValidOptionalHttpUrl(req.TargetUrl))
            return BadRequest("TargetUrl invalide. Utiliser http:// ou https://");

        var banner = new MarketingBanner
        {
            Title = req.Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(req.Subtitle) ? null : req.Subtitle.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            TargetUrl = string.IsNullOrWhiteSpace(req.TargetUrl) ? null : req.TargetUrl.Trim(),
            IsActive = req.IsActive,
            StartAtUtc = req.StartAtUtc,
            EndAtUtc = req.EndAtUtc,
            Language = string.IsNullOrWhiteSpace(req.Language) ? null : req.Language.Trim().ToLowerInvariant(),
            City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim(),
            Zone = string.IsNullOrWhiteSpace(req.Zone) ? null : req.Zone.Trim(),
            CategoryId = req.CategoryId,
            ShopId = req.ShopId,
            SortOrder = req.SortOrder
        };

        _db.MarketingBanners.Add(banner);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(banner));
    }

    [HttpPut("banners/{id:guid}")]
    public async Task<ActionResult<MarketingBannerDto>> Update(Guid id, [FromBody] UpdateMarketingBannerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Titre requis.");
        if (!IsValidPeriod(req.StartAtUtc, req.EndAtUtc))
            return BadRequest("La date de debut doit etre <= a la date de fin.");
        if (!IsValidOptionalHttpUrl(req.TargetUrl))
            return BadRequest("TargetUrl invalide. Utiliser http:// ou https://");

        var banner = await _db.MarketingBanners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return NotFound();

        banner.Title = req.Title.Trim();
        banner.Subtitle = string.IsNullOrWhiteSpace(req.Subtitle) ? null : req.Subtitle.Trim();
        banner.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        banner.TargetUrl = string.IsNullOrWhiteSpace(req.TargetUrl) ? null : req.TargetUrl.Trim();
        banner.IsActive = req.IsActive;
        banner.StartAtUtc = req.StartAtUtc;
        banner.EndAtUtc = req.EndAtUtc;
        banner.Language = string.IsNullOrWhiteSpace(req.Language) ? null : req.Language.Trim().ToLowerInvariant();
        banner.City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim();
        banner.Zone = string.IsNullOrWhiteSpace(req.Zone) ? null : req.Zone.Trim();
        banner.CategoryId = req.CategoryId;
        banner.ShopId = req.ShopId;
        banner.SortOrder = req.SortOrder;
        banner.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(banner));
    }

    [HttpDelete("banners/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var banner = await _db.MarketingBanners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return NotFound();

        _db.MarketingBanners.Remove(banner);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("segments")]
    public async Task<ActionResult<IReadOnlyList<MarketingSegmentDto>>> GetSegments(CancellationToken ct = default)
    {
        var rows = await _db.MarketingSegments.AsNoTracking()
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Name)
            .Select(x => new MarketingSegmentDto(
                x.Id,
                x.Name,
                x.Description,
                x.CriteriaJson,
                x.IsSystem,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("segments")]
    public async Task<ActionResult<MarketingSegmentDto>> UpsertSegment(
        [FromBody] UpsertMarketingSegmentRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Name requis.");
        }

        if (string.IsNullOrWhiteSpace(req.CriteriaJson))
        {
            return BadRequest("CriteriaJson requis.");
        }

        if (!TryNormalizeJson(req.CriteriaJson, out var criteriaJson))
        {
            return BadRequest("CriteriaJson invalide.");
        }

        var now = DateTime.UtcNow;
        MarketingSegment entity;
        if (req.Id.HasValue && req.Id != Guid.Empty)
        {
            entity = await _db.MarketingSegments.FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                ?? new MarketingSegment { Id = req.Id.Value, CreatedAtUtc = now };
            if (_db.Entry(entity).State == EntityState.Detached)
            {
                _db.MarketingSegments.Add(entity);
            }
        }
        else
        {
            entity = new MarketingSegment { CreatedAtUtc = now };
            _db.MarketingSegments.Add(entity);
        }

        entity.Name = req.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        entity.CriteriaJson = criteriaJson;
        entity.IsSystem = req.IsSystem;
        entity.IsActive = req.IsActive;
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        return Ok(new MarketingSegmentDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.CriteriaJson,
            entity.IsSystem,
            entity.IsActive,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc));
    }

    [HttpPost("segments/preview")]
    public async Task<ActionResult<MarketingSegmentPreviewDto>> PreviewSegment(
        [FromBody] PreviewMarketingSegmentRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CriteriaJson))
        {
            return BadRequest("CriteriaJson requis.");
        }

        if (!TryNormalizeJson(req.CriteriaJson, out var criteriaJson))
        {
            return BadRequest("CriteriaJson invalide.");
        }

        var now = DateTime.UtcNow;
        var criteria = ParseCriteria(criteriaJson);
        var sampleSize = Math.Clamp(req.SampleSize ?? 15, 1, 100);
        var usersQuery = BuildCampaignUsersQuery(criteria, now);
        var audienceCount = await usersQuery.CountAsync(ct);

        var sampleUsers = await usersQuery
            .OrderByDescending(u => u.CreatedAtUtc)
            .Select(u => new { u.Id, u.Email, u.CreatedAtUtc })
            .Take(sampleSize)
            .ToListAsync(ct);

        var sampleIds = sampleUsers.Select(x => x.Id).ToList();
        Dictionary<Guid, (int Count, decimal LifetimeSpend, DateTime? LastPaidAtUtc)> paidAgg;
        if (sampleIds.Count == 0)
        {
            paidAgg = new Dictionary<Guid, (int Count, decimal LifetimeSpend, DateTime? LastPaidAtUtc)>();
        }
        else
        {
            // Sample preview is capped (<=100 users), so in-memory aggregation is safe
            // and keeps provider compatibility (SQLite cannot translate Sum(decimal)).
            var paidOrders = await _db.Orders.AsNoTracking()
                .Where(o => o.PaymentStatus == "Paid" && sampleIds.Contains(o.UserId))
                .Select(o => new { o.UserId, o.TotalAmount, o.CreatedAtUtc })
                .ToListAsync(ct);

            paidAgg = paidOrders
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Count: g.Count(),
                        LifetimeSpend: g.Sum(x => x.TotalAmount),
                        LastPaidAtUtc: g.Max(x => (DateTime?)x.CreatedAtUtc)));
        }

        var previewUsers = sampleUsers
            .Select(x =>
            {
                var agg = paidAgg.TryGetValue(x.Id, out var v)
                    ? v
                    : (Count: 0, LifetimeSpend: 0m, LastPaidAtUtc: (DateTime?)null);
                return new MarketingSegmentPreviewUserDto(
                    x.Id,
                    x.Email,
                    agg.Count,
                    agg.LifetimeSpend,
                    agg.LastPaidAtUtc,
                    x.CreatedAtUtc);
            })
            .ToList();

        return Ok(new MarketingSegmentPreviewDto(
            audienceCount,
            criteriaJson,
            previewUsers,
            now));
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<PagedResult<MarketingCampaignDto>>> GetCampaigns(
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var query = _db.MarketingCampaigns.AsNoTracking()
            .Include(x => x.Segment)
            .Include(x => x.Coupon)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(x => x.Name.Contains(s));
        }

        if (normalizedStatus is not null)
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (segmentId.HasValue && segmentId.Value != Guid.Empty)
        {
            query = query.Where(x => x.SegmentId == segmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedChannel))
        {
            query = normalizedChannel switch
            {
                "email" => query.Where(x => x.ChannelEmail),
                "push" => query.Where(x => x.ChannelPush),
                "inapp" => query.Where(x => x.ChannelInApp),
                "multi" => query.Where(x =>
                    (x.ChannelEmail ? 1 : 0) +
                    (x.ChannelPush ? 1 : 0) +
                    (x.ChannelInApp ? 1 : 0) >= 2),
                _ => query
            };
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MarketingCampaignDto(
                x.Id,
                x.Name,
                x.Status,
                x.SegmentId,
                x.Segment != null ? x.Segment.Name : null,
                x.CouponId,
                x.Coupon != null ? x.Coupon.Code : null,
                x.ChannelEmail,
                x.ChannelPush,
                x.ChannelInApp,
                x.MessageTitle,
                x.MessageBody,
                x.StartAtUtc,
                x.EndAtUtc,
                x.LastRunAtUtc,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(ct);

        return Ok(new PagedResult<MarketingCampaignDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpPost("campaigns")]
    public async Task<ActionResult<MarketingCampaignDto>> UpsertCampaign(
        [FromBody] UpsertMarketingCampaignRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Name requis.");
        }

        var normalizedStatus = NormalizeCampaignStatus(req.Status) ?? CampaignStatusDraft;
        if (!IsValidPeriod(req.StartAtUtc, req.EndAtUtc))
        {
            return BadRequest("La date de debut doit etre <= a la date de fin.");
        }

        if (req.SegmentId.HasValue)
        {
            var segmentExists = await _db.MarketingSegments.AsNoTracking()
                .AnyAsync(x => x.Id == req.SegmentId.Value, ct);
            if (!segmentExists)
            {
                return BadRequest("SegmentId invalide.");
            }
        }

        if (req.CouponId.HasValue)
        {
            var couponExists = await _db.Coupons.AsNoTracking()
                .AnyAsync(x => x.Id == req.CouponId.Value, ct);
            if (!couponExists)
            {
                return BadRequest("CouponId invalide.");
            }
        }

        var now = DateTime.UtcNow;
        MarketingCampaign entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.MarketingCampaigns.FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                ?? new MarketingCampaign { Id = req.Id.Value, CreatedAtUtc = now };
            if (_db.Entry(entity).State == EntityState.Detached)
            {
                _db.MarketingCampaigns.Add(entity);
            }
        }
        else
        {
            entity = new MarketingCampaign { CreatedAtUtc = now };
            _db.MarketingCampaigns.Add(entity);
        }

        entity.Name = req.Name.Trim();
        entity.Status = normalizedStatus;
        entity.SegmentId = req.SegmentId;
        entity.CouponId = req.CouponId;
        entity.ChannelEmail = req.ChannelEmail;
        entity.ChannelPush = req.ChannelPush;
        entity.ChannelInApp = req.ChannelInApp;
        entity.MessageTitle = string.IsNullOrWhiteSpace(req.MessageTitle) ? null : req.MessageTitle.Trim();
        entity.MessageBody = string.IsNullOrWhiteSpace(req.MessageBody) ? null : req.MessageBody.Trim();
        entity.StartAtUtc = req.StartAtUtc;
        entity.EndAtUtc = req.EndAtUtc;
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        var dto = await _db.MarketingCampaigns.AsNoTracking()
            .Where(x => x.Id == entity.Id)
            .Include(x => x.Segment)
            .Include(x => x.Coupon)
            .Select(x => new MarketingCampaignDto(
                x.Id,
                x.Name,
                x.Status,
                x.SegmentId,
                x.Segment != null ? x.Segment.Name : null,
                x.CouponId,
                x.Coupon != null ? x.Coupon.Code : null,
                x.ChannelEmail,
                x.ChannelPush,
                x.ChannelInApp,
                x.MessageTitle,
                x.MessageBody,
                x.StartAtUtc,
                x.EndAtUtc,
                x.LastRunAtUtc,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstAsync(ct);

        return Ok(dto);
    }

    [HttpGet("campaigns/{id:guid}/runs")]
    public async Task<ActionResult<IReadOnlyList<MarketingCampaignRunDto>>> GetCampaignRuns(Guid id, CancellationToken ct = default)
    {
        var exists = await _db.MarketingCampaigns.AsNoTracking().AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return NotFound();
        }

        var rows = await _db.MarketingCampaignRuns.AsNoTracking()
            .Where(x => x.CampaignId == id)
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new MarketingCampaignRunDto(
                x.Id,
                x.CampaignId,
                x.RunType,
                x.Status,
                x.TargetUsers,
                x.SentCount,
                x.FailedCount,
                x.RevenueAttributed,
                x.DiscountCost,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Notes))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("campaigns/{id:guid}/run")]
    public async Task<ActionResult<MarketingCampaignRunDto>> RunCampaign(
        Guid id,
        [FromBody] RunMarketingCampaignRequest? req,
        CancellationToken ct = default)
    {
        var campaign = await _db.MarketingCampaigns
            .Include(x => x.Segment)
            .Include(x => x.Coupon)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (campaign is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var runType = req?.RunType?.Trim() is { Length: > 0 } typed ? typed : "Manual";
        var run = await ExecuteCampaignRunAsync(
            campaign,
            runType,
            now,
            attributionWindowDays: 30,
            ct);

        await _db.SaveChangesAsync(ct);

        return Ok(new MarketingCampaignRunDto(
            run.Id,
            run.CampaignId,
            run.RunType,
            run.Status,
            run.TargetUsers,
            run.SentCount,
            run.FailedCount,
            run.RevenueAttributed,
            run.DiscountCost,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.Notes));
    }

    [HttpGet("campaigns/summary")]
    public async Task<ActionResult<MarketingCampaignSummaryDto>> GetCampaignSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var now = DateTime.UtcNow;
        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var totalCampaigns = rows.Count;
        var draft = rows.Count(x => x.Status == CampaignStatusDraft);
        var active = rows.Count(x => x.Status == CampaignStatusActive);
        var paused = rows.Count(x => x.Status == CampaignStatusPaused);
        var completed = rows.Count(x => x.Status == CampaignStatusCompleted);
        var activeInWindow = rows.Count(x =>
            x.Status == CampaignStatusActive &&
            (!x.StartAtUtc.HasValue || x.StartAtUtc.Value <= now) &&
            (!x.EndAtUtc.HasValue || x.EndAtUtc.Value > now));

        var runsCount = rows.Sum(x => x.RunsCount);
        var targetUsers = rows.Sum(x => x.TargetUsers);
        var sentCount = rows.Sum(x => x.SentCount);
        var failedCount = rows.Sum(x => x.FailedCount);
        var revenue = rows.Sum(x => x.RevenueAttributed);
        var cost = rows.Sum(x => x.DiscountCost);
        var roiPercent = cost > 0m
            ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new MarketingCampaignSummaryDto(
            totalCampaigns,
            draft,
            active,
            paused,
            completed,
            activeInWindow,
            runsCount,
            targetUsers,
            sentCount,
            failedCount,
            revenue,
            cost,
            roiPercent,
            fromUtc,
            toUtc));
    }

    [HttpGet("campaigns/roi")]
    public async Task<ActionResult<PagedResult<MarketingCampaignRoiDto>>> GetCampaignRoi(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 500 ? 20 : pageSize;

        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignRoiRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var ordered = rows
            .OrderByDescending(x => x.RoiPercent)
            .ThenByDescending(x => x.RevenueAttributed)
            .ThenBy(x => x.CampaignName)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResult<MarketingCampaignRoiDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("campaigns/roi/export")]
    public async Task<IActionResult> ExportCampaignRoiCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignRoiRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("CampaignId,CampaignName,Status,Segment,Channels,RunsCount,TargetUsers,SentCount,FailedCount,RevenueAttributed,DiscountCost,RoiPercent,LastRunAtUtc");

        foreach (var row in rows
                     .OrderByDescending(x => x.RoiPercent)
                     .ThenByDescending(x => x.RevenueAttributed)
                     .ThenBy(x => x.CampaignName))
        {
            sb.Append(EscapeCsv(row.CampaignId.ToString())).Append(',');
            sb.Append(EscapeCsv(row.CampaignName)).Append(',');
            sb.Append(EscapeCsv(row.Status)).Append(',');
            sb.Append(EscapeCsv(row.SegmentName ?? "-")).Append(',');
            sb.Append(EscapeCsv(row.Channels)).Append(',');
            sb.Append(row.RunsCount).Append(',');
            sb.Append(row.TargetUsers).Append(',');
            sb.Append(row.SentCount).Append(',');
            sb.Append(row.FailedCount).Append(',');
            sb.Append(row.RevenueAttributed.ToString("0.00")).Append(',');
            sb.Append(row.DiscountCost.ToString("0.00")).Append(',');
            sb.Append(row.RoiPercent.ToString("0.00")).Append(',');
            sb.Append(EscapeCsv(row.LastRunAtUtc?.ToString("O") ?? string.Empty));
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"campaign-roi-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet("campaigns/cohorts")]
    public async Task<ActionResult<IReadOnlyList<MarketingCampaignCohortDto>>> GetCampaignCohorts(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var totalSent = rows.Sum(x => x.SentCount);
        var totalRevenue = rows.Sum(x => x.RevenueAttributed);

        var cohorts = rows
            .GroupBy(x => new { x.SegmentId, SegmentName = string.IsNullOrWhiteSpace(x.SegmentName) ? "Unsegmented" : x.SegmentName! })
            .Select(g =>
            {
                var runsCount = g.Sum(x => x.RunsCount);
                var sent = g.Sum(x => x.SentCount);
                var revenue = g.Sum(x => x.RevenueAttributed);
                var cost = g.Sum(x => x.DiscountCost);
                var roiPercent = cost > 0m
                    ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                var shareSentPercent = totalSent > 0
                    ? decimal.Round((decimal)sent * 100m / totalSent, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var shareRevenuePercent = totalRevenue > 0m
                    ? decimal.Round(revenue * 100m / totalRevenue, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new MarketingCampaignCohortDto(
                    g.Key.SegmentId,
                    g.Key.SegmentName,
                    g.Count(),
                    runsCount,
                    sent,
                    revenue,
                    cost,
                    roiPercent,
                    shareSentPercent,
                    shareRevenuePercent);
            })
            .OrderByDescending(x => x.RevenueAttributed)
            .ThenByDescending(x => x.SentCount)
            .ThenBy(x => x.SegmentName)
            .ToList();

        return Ok(cohorts);
    }

    [HttpGet("campaigns/timeseries")]
    public async Task<ActionResult<MarketingCampaignTimeSeriesDto>> GetCampaignTimeSeries(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        [FromQuery] string? granularity = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var normalizedGranularity = NormalizeGranularity(granularity);
        if (granularity is not null && normalizedGranularity is null)
        {
            return BadRequest("Granularity invalide. Utiliser day ou week.");
        }

        var effectiveGranularity = normalizedGranularity ?? "day";
        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var campaignRows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);
        var points = await BuildCampaignTimeSeriesPointsAsync(
            campaignRows,
            fromUtc,
            toUtc,
            effectiveGranularity,
            ct);

        return Ok(new MarketingCampaignTimeSeriesDto(
            effectiveGranularity,
            fromUtc,
            toUtc,
            points));
    }

    [HttpGet("campaigns/experiments")]
    public async Task<ActionResult<IReadOnlyList<MarketingCampaignExperimentDto>>> GetCampaignExperiments(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);
        var grouped = await BuildCampaignExperimentRowsAsync(rows, fromUtc, toUtc, ct);

        return Ok(grouped);
    }

    [HttpGet("campaigns/experiments/export")]
    public async Task<IActionResult> ExportCampaignExperimentsCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var result = await GetCampaignExperiments(
            from,
            to,
            q,
            status,
            segmentId,
            channel,
            minSent,
            minRoi,
            ct);

        if (result.Result is ObjectResult objectResult && (objectResult.StatusCode ?? 500) >= 400)
        {
            return objectResult;
        }

        IReadOnlyList<MarketingCampaignExperimentDto> rows;
        if (result.Result is OkObjectResult okResult &&
            okResult.Value is IReadOnlyList<MarketingCampaignExperimentDto> okRows)
        {
            rows = okRows;
        }
        else
        {
            rows = result.Value ?? Array.Empty<MarketingCampaignExperimentDto>();
        }
        var sb = new StringBuilder();
        sb.AppendLine("Experiment,CampaignsCount,RunsCount,TargetUsers,SentCount,FailedCount,RevenueAttributed,DiscountCost,RoiPercent,DeliveryRatePercent,AvgRevenuePerSent,FirstRunAtUtc,LastRunAtUtc");

        foreach (var row in rows)
        {
            sb.Append(EscapeCsv(row.Experiment)).Append(',');
            sb.Append(row.CampaignsCount).Append(',');
            sb.Append(row.RunsCount).Append(',');
            sb.Append(row.TargetUsers).Append(',');
            sb.Append(row.SentCount).Append(',');
            sb.Append(row.FailedCount).Append(',');
            sb.Append(row.RevenueAttributed.ToString("0.00")).Append(',');
            sb.Append(row.DiscountCost.ToString("0.00")).Append(',');
            sb.Append(row.RoiPercent.ToString("0.00")).Append(',');
            sb.Append(row.DeliveryRatePercent.ToString("0.00")).Append(',');
            sb.Append(row.AvgRevenuePerSent.ToString("0.0000")).Append(',');
            sb.Append(EscapeCsv(row.FirstRunAtUtc.ToString("O"))).Append(',');
            sb.Append(EscapeCsv(row.LastRunAtUtc.ToString("O")));
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"campaign-experiments-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet("campaigns/experiments/insights")]
    public async Task<ActionResult<MarketingCampaignExperimentInsightsDto>> GetCampaignExperimentInsights(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var experiments = await BuildCampaignExperimentRowsAsync(rows, fromUtc, toUtc, ct);

        var candidates = experiments
            .Where(x => x.SentCount > 0)
            .OrderByDescending(x => x.SentCount)
            .ToList();

        MarketingCampaignExperimentDto? baseline = null;
        MarketingCampaignExperimentDto? variant = null;
        if (candidates.Count >= 2)
        {
            baseline = candidates
                .OrderBy(x => ExperimentPriority(x.Experiment))
                .ThenByDescending(x => x.SentCount)
                .First();
            variant = candidates
                .Where(x => !string.Equals(x.Experiment, baseline.Experiment, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.SentCount)
                .ThenByDescending(x => x.RoiPercent)
                .FirstOrDefault();
        }

        if (baseline is null || variant is null)
        {
            var fallbackRecommendations = new List<MarketingActionRecommendationDto>
            {
                new(
                    "High",
                    "Collect more experiment data",
                    "At least two experiment groups with sent volume are required for automatic uplift.",
                    "Keep campaign experiment tracking enabled for 7 days and refresh insights.")
            };

            return Ok(new MarketingCampaignExperimentInsightsDto(
                fromUtc,
                toUtc,
                false,
                null,
                null,
                0m,
                0m,
                0m,
                "Low",
                "None",
                fallbackRecommendations,
                experiments));
        }

        var upliftRoiPct = decimal.Round(
            variant.RoiPercent - baseline.RoiPercent,
            2,
            MidpointRounding.AwayFromZero);

        var upliftRevenuePerSentPct = baseline.AvgRevenuePerSent > 0m
            ? decimal.Round(
                (variant.AvgRevenuePerSent - baseline.AvgRevenuePerSent) * 100m / baseline.AvgRevenuePerSent,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        var deliveryDeltaPct = decimal.Round(
            variant.DeliveryRatePercent - baseline.DeliveryRatePercent,
            2,
            MidpointRounding.AwayFromZero);

        var confidence = ResolveUpliftConfidence(baseline.SentCount, variant.SentCount);
        var winner = upliftRoiPct > 0m
            ? variant.Experiment
            : upliftRoiPct < 0m
                ? baseline.Experiment
                : "Tie";

        var recommendations = BuildExperimentRecommendations(
            baseline,
            variant,
            upliftRoiPct,
            upliftRevenuePerSentPct,
            deliveryDeltaPct,
            confidence);

        return Ok(new MarketingCampaignExperimentInsightsDto(
            fromUtc,
            toUtc,
            true,
            baseline,
            variant,
            upliftRoiPct,
            upliftRevenuePerSentPct,
            deliveryDeltaPct,
            confidence,
            winner,
            recommendations,
            experiments));
    }

    [HttpGet("campaigns/experiments/insights/export")]
    public async Task<IActionResult> ExportCampaignExperimentInsightsCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var result = await GetCampaignExperimentInsights(
            from,
            to,
            q,
            status,
            segmentId,
            channel,
            minSent,
            minRoi,
            ct);

        if (result.Result is ObjectResult objectResult && (objectResult.StatusCode ?? 500) >= 400)
        {
            return objectResult;
        }

        MarketingCampaignExperimentInsightsDto? payload = null;
        if (result.Result is OkObjectResult okResult &&
            okResult.Value is MarketingCampaignExperimentInsightsDto okPayload)
        {
            payload = okPayload;
        }
        else
        {
            payload = result.Value;
        }

        payload ??= new MarketingCampaignExperimentInsightsDto(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            false,
            null,
            null,
            0m,
            0m,
            0m,
            "Low",
            "None",
            Array.Empty<MarketingActionRecommendationDto>(),
            Array.Empty<MarketingCampaignExperimentDto>());

        var sb = new StringBuilder();
        sb.AppendLine("Section,Metric,Value");
        sb.AppendLine($"Summary,FromUtc,{payload.FromUtc:O}");
        sb.AppendLine($"Summary,ToUtc,{payload.ToUtc:O}");
        sb.AppendLine($"Summary,HasPair,{payload.HasPair}");
        sb.AppendLine($"Summary,Baseline,{EscapeCsv(payload.Baseline?.Experiment ?? "None")}");
        sb.AppendLine($"Summary,Variant,{EscapeCsv(payload.Variant?.Experiment ?? "None")}");
        sb.AppendLine($"Summary,UpliftRoiPct,{payload.UpliftRoiPct:0.00}");
        sb.AppendLine($"Summary,UpliftRevenuePerSentPct,{payload.UpliftRevenuePerSentPct:0.00}");
        sb.AppendLine($"Summary,DeliveryDeltaPct,{payload.DeliveryDeltaPct:0.00}");
        sb.AppendLine($"Summary,Confidence,{EscapeCsv(payload.Confidence)}");
        sb.AppendLine($"Summary,Winner,{EscapeCsv(payload.WinnerExperiment)}");
        sb.AppendLine();

        sb.AppendLine("Section,Priority,Action,Rationale,ExecutionHint");
        foreach (var rec in payload.Recommendations)
        {
            sb.Append("Recommendation,")
                .Append(EscapeCsv(rec.Priority)).Append(',')
                .Append(EscapeCsv(rec.Action)).Append(',')
                .Append(EscapeCsv(rec.Rationale)).Append(',')
                .Append(EscapeCsv(rec.ExecutionHint))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Section,Experiment,CampaignsCount,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent,DeliveryRatePercent");
        foreach (var row in payload.Experiments)
        {
            sb.Append("Experiment,")
                .Append(EscapeCsv(row.Experiment)).Append(',')
                .Append(row.CampaignsCount).Append(',')
                .Append(row.RunsCount).Append(',')
                .Append(row.SentCount).Append(',')
                .Append(row.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(row.DiscountCost.ToString("0.00")).Append(',')
                .Append(row.RoiPercent.ToString("0.00")).Append(',')
                .Append(row.DeliveryRatePercent.ToString("0.00"))
                .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"campaign-experiment-insights-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet("campaigns/kpi/export")]
    public async Task<IActionResult> ExportCampaignKpiCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var now = DateTime.UtcNow;
        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var draft = rows.Count(x => x.Status == CampaignStatusDraft);
        var active = rows.Count(x => x.Status == CampaignStatusActive);
        var paused = rows.Count(x => x.Status == CampaignStatusPaused);
        var completed = rows.Count(x => x.Status == CampaignStatusCompleted);
        var activeInWindow = rows.Count(x =>
            x.Status == CampaignStatusActive &&
            (!x.StartAtUtc.HasValue || x.StartAtUtc.Value <= now) &&
            (!x.EndAtUtc.HasValue || x.EndAtUtc.Value > now));
        var runsCount = rows.Sum(x => x.RunsCount);
        var targetUsers = rows.Sum(x => x.TargetUsers);
        var sentCount = rows.Sum(x => x.SentCount);
        var failedCount = rows.Sum(x => x.FailedCount);
        var revenue = rows.Sum(x => x.RevenueAttributed);
        var cost = rows.Sum(x => x.DiscountCost);
        var roiPercent = cost > 0m
            ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var cohorts = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SegmentName) ? "Unsegmented" : x.SegmentName!)
            .Select(g =>
            {
                var cohortRevenue = g.Sum(x => x.RevenueAttributed);
                var cohortCost = g.Sum(x => x.DiscountCost);
                var cohortRoi = cohortCost > 0m
                    ? decimal.Round((cohortRevenue - cohortCost) * 100m / cohortCost, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new
                {
                    SegmentName = g.Key,
                    CampaignCount = g.Count(),
                    RunsCount = g.Sum(x => x.RunsCount),
                    SentCount = g.Sum(x => x.SentCount),
                    Revenue = cohortRevenue,
                    Cost = cohortCost,
                    Roi = cohortRoi
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Section,Metric,Value");
        sb.AppendLine($"Summary,FromUtc,{fromUtc:O}");
        sb.AppendLine($"Summary,ToUtc,{toUtc:O}");
        sb.AppendLine($"Summary,TotalCampaigns,{rows.Count}");
        sb.AppendLine($"Summary,DraftCampaigns,{draft}");
        sb.AppendLine($"Summary,ActiveCampaigns,{active}");
        sb.AppendLine($"Summary,PausedCampaigns,{paused}");
        sb.AppendLine($"Summary,CompletedCampaigns,{completed}");
        sb.AppendLine($"Summary,ActiveInWindow,{activeInWindow}");
        sb.AppendLine($"Summary,RunsCount,{runsCount}");
        sb.AppendLine($"Summary,TargetUsers,{targetUsers}");
        sb.AppendLine($"Summary,SentCount,{sentCount}");
        sb.AppendLine($"Summary,FailedCount,{failedCount}");
        sb.AppendLine($"Summary,RevenueAttributed,{revenue:0.00}");
        sb.AppendLine($"Summary,DiscountCost,{cost:0.00}");
        sb.AppendLine($"Summary,RoiPercent,{roiPercent:0.00}");
        sb.AppendLine();

        sb.AppendLine("Section,Segment,CampaignCount,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var cohort in cohorts)
        {
            sb.Append("Cohort,")
                .Append(EscapeCsv(cohort.SegmentName)).Append(',')
                .Append(cohort.CampaignCount).Append(',')
                .Append(cohort.RunsCount).Append(',')
                .Append(cohort.SentCount).Append(',')
                .Append(cohort.Revenue.ToString("0.00")).Append(',')
                .Append(cohort.Cost.ToString("0.00")).Append(',')
                .Append(cohort.Roi.ToString("0.00"))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Section,CampaignId,CampaignName,Status,Segment,Channels,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var row in rows
                     .OrderByDescending(x => x.RoiPercent)
                     .ThenByDescending(x => x.RevenueAttributed)
                     .ThenBy(x => x.CampaignName))
        {
            sb.Append("Campaign,")
                .Append(EscapeCsv(row.CampaignId.ToString())).Append(',')
                .Append(EscapeCsv(row.CampaignName)).Append(',')
                .Append(EscapeCsv(row.Status)).Append(',')
                .Append(EscapeCsv(row.SegmentName ?? "-")).Append(',')
                .Append(EscapeCsv(BuildCampaignChannels(row.ChannelEmail, row.ChannelPush, row.ChannelInApp))).Append(',')
                .Append(row.RunsCount).Append(',')
                .Append(row.SentCount).Append(',')
                .Append(row.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(row.DiscountCost.ToString("0.00")).Append(',')
                .Append(row.RoiPercent.ToString("0.00"))
                .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"campaign-kpi-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet("campaigns/dashboard/export")]
    public async Task<IActionResult> ExportCampaignDashboardCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? segmentId = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? minSent = null,
        [FromQuery] decimal? minRoi = null,
        [FromQuery] string? granularity = null,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeCampaignStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return BadRequest("Status campagne invalide.");
        }

        var normalizedChannel = NormalizeCampaignChannel(channel);
        if (channel is not null && normalizedChannel is null)
        {
            return BadRequest("Canal campagne invalide.");
        }

        var normalizedGranularity = NormalizeGranularity(granularity);
        if (granularity is not null && normalizedGranularity is null)
        {
            return BadRequest("Granularity invalide. Utiliser day ou week.");
        }

        var effectiveGranularity = normalizedGranularity ?? "day";
        var now = DateTime.UtcNow;
        var (fromUtc, toUtc) = NormalizeRange(from, to, 30);
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        var timeseries = await BuildCampaignTimeSeriesPointsAsync(
            rows,
            fromUtc,
            toUtc,
            effectiveGranularity,
            ct);

        var draft = rows.Count(x => x.Status == CampaignStatusDraft);
        var active = rows.Count(x => x.Status == CampaignStatusActive);
        var paused = rows.Count(x => x.Status == CampaignStatusPaused);
        var completed = rows.Count(x => x.Status == CampaignStatusCompleted);
        var activeInWindow = rows.Count(x =>
            x.Status == CampaignStatusActive &&
            (!x.StartAtUtc.HasValue || x.StartAtUtc.Value <= now) &&
            (!x.EndAtUtc.HasValue || x.EndAtUtc.Value > now));
        var runsCount = rows.Sum(x => x.RunsCount);
        var targetUsers = rows.Sum(x => x.TargetUsers);
        var sentCount = rows.Sum(x => x.SentCount);
        var failedCount = rows.Sum(x => x.FailedCount);
        var revenue = rows.Sum(x => x.RevenueAttributed);
        var cost = rows.Sum(x => x.DiscountCost);
        var roiPercent = cost > 0m
            ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var deliverySuccessRate = (sentCount + failedCount) > 0
            ? decimal.Round((decimal)sentCount * 100m / (sentCount + failedCount), 2, MidpointRounding.AwayFromZero)
            : 0m;
        var avgRevenuePerSent = sentCount > 0
            ? decimal.Round(revenue / sentCount, 4, MidpointRounding.AwayFromZero)
            : 0m;
        var avgCostPerSent = sentCount > 0
            ? decimal.Round(cost / sentCount, 4, MidpointRounding.AwayFromZero)
            : 0m;

        var cohorts = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SegmentName) ? "Unsegmented" : x.SegmentName!)
            .Select(g =>
            {
                var cohortRevenue = g.Sum(x => x.RevenueAttributed);
                var cohortCost = g.Sum(x => x.DiscountCost);
                var cohortRoi = cohortCost > 0m
                    ? decimal.Round((cohortRevenue - cohortCost) * 100m / cohortCost, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new
                {
                    SegmentName = g.Key,
                    CampaignCount = g.Count(),
                    RunsCount = g.Sum(x => x.RunsCount),
                    SentCount = g.Sum(x => x.SentCount),
                    Revenue = cohortRevenue,
                    Cost = cohortCost,
                    Roi = cohortRoi
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var ranked = rows
            .Where(x => x.SentCount > 0)
            .OrderByDescending(x => x.RoiPercent)
            .ThenByDescending(x => x.RevenueAttributed)
            .ThenBy(x => x.CampaignName)
            .ToList();
        var topCampaigns = ranked.Take(5).ToList();
        var bottomCampaigns = ranked
            .OrderBy(x => x.RoiPercent)
            .ThenByDescending(x => x.SentCount)
            .ThenBy(x => x.CampaignName)
            .Take(5)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Section,Metric,Value");
        sb.AppendLine($"Summary,FromUtc,{fromUtc:O}");
        sb.AppendLine($"Summary,ToUtc,{toUtc:O}");
        sb.AppendLine($"Summary,Granularity,{effectiveGranularity}");
        sb.AppendLine($"Summary,TotalCampaigns,{rows.Count}");
        sb.AppendLine($"Summary,DraftCampaigns,{draft}");
        sb.AppendLine($"Summary,ActiveCampaigns,{active}");
        sb.AppendLine($"Summary,PausedCampaigns,{paused}");
        sb.AppendLine($"Summary,CompletedCampaigns,{completed}");
        sb.AppendLine($"Summary,ActiveInWindow,{activeInWindow}");
        sb.AppendLine($"Summary,RunsCount,{runsCount}");
        sb.AppendLine($"Summary,TargetUsers,{targetUsers}");
        sb.AppendLine($"Summary,SentCount,{sentCount}");
        sb.AppendLine($"Summary,FailedCount,{failedCount}");
        sb.AppendLine($"Summary,RevenueAttributed,{revenue:0.00}");
        sb.AppendLine($"Summary,DiscountCost,{cost:0.00}");
        sb.AppendLine($"Summary,RoiPercent,{roiPercent:0.00}");
        sb.AppendLine($"Summary,DeliverySuccessRate,{deliverySuccessRate:0.00}");
        sb.AppendLine($"Summary,AvgRevenuePerSent,{avgRevenuePerSent:0.0000}");
        sb.AppendLine($"Summary,AvgCostPerSent,{avgCostPerSent:0.0000}");
        sb.AppendLine();

        sb.AppendLine("Section,PeriodStartUtc,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var point in timeseries)
        {
            sb.Append("TimeSeries,")
                .Append(EscapeCsv(point.PeriodStartUtc.ToString("O"))).Append(',')
                .Append(point.RunsCount).Append(',')
                .Append(point.SentCount).Append(',')
                .Append(point.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(point.DiscountCost.ToString("0.00")).Append(',')
                .Append(point.RoiPercent.ToString("0.00"))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Section,Segment,CampaignCount,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var cohort in cohorts)
        {
            sb.Append("Cohort,")
                .Append(EscapeCsv(cohort.SegmentName)).Append(',')
                .Append(cohort.CampaignCount).Append(',')
                .Append(cohort.RunsCount).Append(',')
                .Append(cohort.SentCount).Append(',')
                .Append(cohort.Revenue.ToString("0.00")).Append(',')
                .Append(cohort.Cost.ToString("0.00")).Append(',')
                .Append(cohort.Roi.ToString("0.00"))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Section,Type,CampaignId,CampaignName,Status,Segment,Channels,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var row in topCampaigns)
        {
            sb.Append("Insights,Top,")
                .Append(EscapeCsv(row.CampaignId.ToString())).Append(',')
                .Append(EscapeCsv(row.CampaignName)).Append(',')
                .Append(EscapeCsv(row.Status)).Append(',')
                .Append(EscapeCsv(row.SegmentName ?? "-")).Append(',')
                .Append(EscapeCsv(BuildCampaignChannels(row.ChannelEmail, row.ChannelPush, row.ChannelInApp))).Append(',')
                .Append(row.RunsCount).Append(',')
                .Append(row.SentCount).Append(',')
                .Append(row.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(row.DiscountCost.ToString("0.00")).Append(',')
                .Append(row.RoiPercent.ToString("0.00"))
                .AppendLine();
        }

        foreach (var row in bottomCampaigns)
        {
            sb.Append("Insights,Bottom,")
                .Append(EscapeCsv(row.CampaignId.ToString())).Append(',')
                .Append(EscapeCsv(row.CampaignName)).Append(',')
                .Append(EscapeCsv(row.Status)).Append(',')
                .Append(EscapeCsv(row.SegmentName ?? "-")).Append(',')
                .Append(EscapeCsv(BuildCampaignChannels(row.ChannelEmail, row.ChannelPush, row.ChannelInApp))).Append(',')
                .Append(row.RunsCount).Append(',')
                .Append(row.SentCount).Append(',')
                .Append(row.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(row.DiscountCost.ToString("0.00")).Append(',')
                .Append(row.RoiPercent.ToString("0.00"))
                .AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Section,CampaignId,CampaignName,Status,Segment,Channels,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent");
        foreach (var row in rows
                     .OrderByDescending(x => x.RoiPercent)
                     .ThenByDescending(x => x.RevenueAttributed)
                     .ThenBy(x => x.CampaignName))
        {
            sb.Append("Campaign,")
                .Append(EscapeCsv(row.CampaignId.ToString())).Append(',')
                .Append(EscapeCsv(row.CampaignName)).Append(',')
                .Append(EscapeCsv(row.Status)).Append(',')
                .Append(EscapeCsv(row.SegmentName ?? "-")).Append(',')
                .Append(EscapeCsv(BuildCampaignChannels(row.ChannelEmail, row.ChannelPush, row.ChannelInApp))).Append(',')
                .Append(row.RunsCount).Append(',')
                .Append(row.SentCount).Append(',')
                .Append(row.RevenueAttributed.ToString("0.00")).Append(',')
                .Append(row.DiscountCost.ToString("0.00")).Append(',')
                .Append(row.RoiPercent.ToString("0.00"))
                .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"campaign-dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpPost("automation/run-campaigns")]
    public async Task<ActionResult<MarketingCampaignBatchAutomationResultDto>> RunCampaignAutomation(
        [FromBody] RunCampaignBatchAutomationRequest? req,
        CancellationToken ct = default)
    {
        req ??= new RunCampaignBatchAutomationRequest(null, null, null, null, null, null);

        var now = DateTime.UtcNow;
        var dryRun = req.DryRun ?? false;
        var includeDraft = req.IncludeDraft ?? false;
        var autoCompleteExpired = req.AutoCompleteExpired ?? true;
        var maxCampaigns = Math.Clamp(req.MaxCampaigns ?? 20, 1, 200);
        var attributionWindowDays = Math.Clamp(req.AttributionWindowDays ?? 30, 1, 180);
        var runType = string.IsNullOrWhiteSpace(req.RunType) ? "Scheduled" : req.RunType.Trim();

        var eligibleStatuses = includeDraft
            ? new[] { CampaignStatusActive, CampaignStatusDraft }
            : new[] { CampaignStatusActive };

        var campaigns = await _db.MarketingCampaigns
            .Include(x => x.Segment)
            .Include(x => x.Coupon)
            .Where(x => eligibleStatuses.Contains(x.Status))
            .Where(x => !x.StartAtUtc.HasValue || x.StartAtUtc <= now)
            .Where(x => !x.EndAtUtc.HasValue || x.EndAtUtc > now)
            .OrderBy(x => x.LastRunAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(maxCampaigns)
            .ToListAsync(ct);

        var completedExpiredCampaigns = 0;
        if (autoCompleteExpired)
        {
            var expirable = await _db.MarketingCampaigns
                .Where(x =>
                    (x.Status == CampaignStatusActive || x.Status == CampaignStatusDraft || x.Status == CampaignStatusPaused) &&
                    x.EndAtUtc.HasValue &&
                    x.EndAtUtc.Value <= now)
                .ToListAsync(ct);

            completedExpiredCampaigns = expirable.Count;
            if (!dryRun)
            {
                foreach (var campaign in expirable)
                {
                    campaign.Status = CampaignStatusCompleted;
                    campaign.UpdatedAtUtc = now;
                }
            }
        }

        var processedCampaigns = 0;
        var createdRuns = 0;
        var totalTargetUsers = 0;
        var totalSent = 0;
        var totalFailed = 0;
        decimal totalRevenue = 0m;
        decimal totalCost = 0m;

        if (!dryRun)
        {
            foreach (var campaign in campaigns)
            {
                var run = await ExecuteCampaignRunAsync(
                    campaign,
                    runType,
                    now,
                    attributionWindowDays,
                    ct);

                processedCampaigns++;
                createdRuns++;
                totalTargetUsers += run.TargetUsers;
                totalSent += run.SentCount;
                totalFailed += run.FailedCount;
                totalRevenue += run.RevenueAttributed;
                totalCost += run.DiscountCost;
            }

            await _db.SaveChangesAsync(ct);
        }
        else
        {
            processedCampaigns = campaigns.Count;
        }

        var roiPercent = totalCost > 0m
            ? decimal.Round((totalRevenue - totalCost) * 100m / totalCost, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new MarketingCampaignBatchAutomationResultDto(
            campaigns.Count,
            processedCampaigns,
            createdRuns,
            completedExpiredCampaigns,
            totalTargetUsers,
            totalSent,
            totalFailed,
            totalRevenue,
            totalCost,
            roiPercent,
            dryRun,
            now));
    }

    [HttpGet("segments/rfm-insights")]
    public async Task<ActionResult<MarketingRfmInsightsDto>> GetRfmInsights(
        [FromQuery] int windowDays = 180,
        CancellationToken ct = default)
    {
        windowDays = Math.Clamp(windowDays, 30, 365);
        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-windowDays);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Client && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var paidOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == "Paid")
            .Select(o => new PaidOrderPoint(
                o.UserId,
                o.CreatedAtUtc,
                o.TotalAmount))
            .ToListAsync(ct);

        var paidByUser = paidOrders
            .GroupBy(o => o.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAtUtc).ToList());

        var rows = new List<MarketingRfmUserRowDto>(users.Count);
        foreach (var userId in users)
        {
            paidByUser.TryGetValue(userId, out var userOrders);
            userOrders ??= new List<PaidOrderPoint>();

            var paidCountAll = userOrders.Count;
            var lastPaidAtUtc = userOrders.Count > 0 ? (DateTime?)userOrders[0].CreatedAtUtc : null;
            var paidInWindow = userOrders.Where(x => x.CreatedAtUtc >= fromUtc).ToList();
            var frequency = paidInWindow.Count;
            var monetary = paidInWindow.Sum(x => (decimal)x.TotalAmount);
            var daysSinceLast = lastPaidAtUtc.HasValue ? (int)Math.Floor((now - lastPaidAtUtc.Value).TotalDays) : int.MaxValue;
            var tag = ClassifyRfmTag(daysSinceLast, frequency, monetary, paidCountAll);

            rows.Add(new MarketingRfmUserRowDto(
                userId,
                tag,
                daysSinceLast == int.MaxValue ? null : daysSinceLast,
                frequency,
                monetary,
                lastPaidAtUtc));
        }

        var tagStats = rows
            .GroupBy(x => x.Tag)
            .OrderBy(g => g.Key)
            .Select(g => new MarketingRfmTagStatDto(
                g.Key,
                g.Count(),
                g.Count() == 0 ? 0m : decimal.Round((decimal)g.Count() * 100m / rows.Count, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return Ok(new MarketingRfmInsightsDto(
            now,
            fromUtc,
            users.Count,
            rows.Count(x => x.LastPaidAtUtc.HasValue),
            rows.Count(x => !x.LastPaidAtUtc.HasValue),
            tagStats,
            rows));
    }

    [HttpPost("segments/rfm-bootstrap")]
    public async Task<ActionResult<MarketingRfmBootstrapResultDto>> BootstrapRfmSegments(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var (created, updated, total) = await UpsertRfmSystemSegmentsAsync(now, ct);

        await _db.SaveChangesAsync(ct);

        return Ok(new MarketingRfmBootstrapResultDto(
            created,
            updated,
            total,
            now));
    }

    [HttpPost("automation/run-scenarios")]
    public async Task<ActionResult<MarketingScenarioAutomationResultDto>> RunScenarioAutomation(
        [FromBody] RunMarketingScenarioAutomationRequest? req,
        CancellationToken ct = default)
    {
        req ??= new RunMarketingScenarioAutomationRequest(null, null, null, null, null, null, null, null, null, null, null, null);

        var now = DateTime.UtcNow;
        var dryRun = req.DryRun ?? false;
        var includeWelcome = req.IncludeWelcome ?? true;
        var includeWinback = req.IncludeWinback ?? true;
        var includeChurnRisk = req.IncludeChurnRisk ?? true;
        var maxScenarios = Math.Clamp(req.MaxScenarios ?? 20, 1, 100);
        var attributionWindowDays = Math.Clamp(req.AttributionWindowDays ?? 30, 1, 180);
        var antiSpamStep1Hours = Math.Clamp(req.AntiSpamStep1Hours ?? 24, 1, 168);
        var antiSpamStep2Hours = Math.Clamp(req.AntiSpamStep2Hours ?? 48, 1, 336);
        var antiSpamStep3Hours = Math.Clamp(req.AntiSpamStep3Hours ?? 72, 1, 504);
        var scenarioWindowDays = Math.Clamp(req.ScenarioWindowDays ?? 7, 1, 90);
        var scenarioMaxTouchesPerUser = Math.Clamp(req.ScenarioMaxTouchesPerUser ?? 3, 1, 20);
        var splitChannelsToCampaigns = req.SplitChannelsToCampaigns ?? false;

        // Ensure RFM system segments exist before scenario resolution.
        var (rfmCreated, rfmUpdated, _) = await UpsertRfmSystemSegmentsAsync(now, ct);
        if (rfmCreated > 0 || rfmUpdated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        var scenarios = new List<ScenarioPlan>();
        if (includeWelcome)
        {
            scenarios.Add(new ScenarioPlan(
                "welcome",
                "Scenario Welcome",
                "RFM - New",
                new[]
                {
                    new ScenarioStep("S1", "Bienvenue - activation", "Active", true, false, true),
                    new ScenarioStep("S2", "Incitation premiere recurrence", "Active", true, true, true),
                    new ScenarioStep("S3", "Reminder engagement", "Active", true, true, true)
                }));
        }

        if (includeWinback)
        {
            scenarios.Add(new ScenarioPlan(
                "winback",
                "Scenario Winback",
                "RFM - Hibernating",
                new[]
                {
                    new ScenarioStep("S1", "We miss you", "Active", true, true, true),
                    new ScenarioStep("S2", "Coupon relance", "Active", true, true, true),
                    new ScenarioStep("S3", "Derniere relance", "Active", true, true, true)
                }));
        }

        if (includeChurnRisk)
        {
            scenarios.Add(new ScenarioPlan(
                "churn",
                "Scenario Churn Risk",
                "RFM - At Risk",
                new[]
                {
                    new ScenarioStep("S1", "Engagement check", "Active", true, true, true),
                    new ScenarioStep("S2", "Retention offer", "Active", true, true, true),
                    new ScenarioStep("S3", "Escalation retention", "Active", true, true, true)
                }));
        }

        scenarios = scenarios.Take(maxScenarios).ToList();

        var ensuredCampaigns = 0;
        var executedRuns = 0;
        var stepsExecuted = new List<MarketingScenarioStepExecutionDto>();
        var totalTargetUsers = 0;
        var totalSent = 0;
        var totalFailed = 0;
        var antiSpamSkippedUsers = 0;
        decimal totalRevenue = 0m;
        decimal totalCost = 0m;
        var currentRunScenarioTouchCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var currentRunStepTouches = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var scenario in scenarios)
        {
            var segment = await _db.MarketingSegments
                .FirstOrDefaultAsync(x => x.Name == scenario.SegmentName && x.IsSystem, ct);
            if (segment is null)
            {
                continue;
            }

            var executionPlans = new List<ScenarioCampaignExecutionPlan>();
            for (var stepIndex = 0; stepIndex < scenario.Steps.Count; stepIndex++)
            {
                var step = scenario.Steps[stepIndex];
                var channelSpecs = BuildScenarioChannelSpecs(step, splitChannelsToCampaigns);
                foreach (var channelSpec in channelSpecs)
                {
                    var campaignName = splitChannelsToCampaigns
                        ? $"{scenario.Prefix} - {step.Code} - {channelSpec.Label}"
                        : $"{scenario.Prefix} - {step.Code}";

                    var campaign = await _db.MarketingCampaigns
                        .Include(x => x.Segment)
                        .Include(x => x.Coupon)
                        .FirstOrDefaultAsync(x => x.Name == campaignName, ct);

                    if (campaign is null)
                    {
                        campaign = new MarketingCampaign
                        {
                            Name = campaignName,
                            Status = step.Status,
                            SegmentId = segment.Id,
                            ChannelEmail = channelSpec.ChannelEmail,
                            ChannelPush = channelSpec.ChannelPush,
                            ChannelInApp = channelSpec.ChannelInApp,
                            MessageTitle = splitChannelsToCampaigns
                                ? $"{scenario.Label} {step.Code} {channelSpec.Label}"
                                : $"{scenario.Label} {step.Code}",
                            MessageBody = step.MessageBody,
                            StartAtUtc = now.AddDays(-1),
                            EndAtUtc = now.AddMonths(6),
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        };
                        _db.MarketingCampaigns.Add(campaign);
                    }
                    else
                    {
                        campaign.Status = step.Status;
                        campaign.SegmentId = segment.Id;
                        campaign.ChannelEmail = channelSpec.ChannelEmail;
                        campaign.ChannelPush = channelSpec.ChannelPush;
                        campaign.ChannelInApp = channelSpec.ChannelInApp;
                        campaign.MessageTitle = splitChannelsToCampaigns
                            ? $"{scenario.Label} {step.Code} {channelSpec.Label}"
                            : $"{scenario.Label} {step.Code}";
                        campaign.MessageBody = step.MessageBody;
                        campaign.UpdatedAtUtc = now;
                    }

                    ensuredCampaigns++;
                    executionPlans.Add(new ScenarioCampaignExecutionPlan(
                        stepIndex,
                        step.Code,
                        channelSpec.Code,
                        campaign));
                }
            }

            if (dryRun)
            {
                continue;
            }

            // Multi-step progression:
            // Step1 if never run in last antiSpamStep1Hours.
            // Step2 if Step1 ran and Step2 not in last antiSpamStep2Hours.
            // Step3 if Step2 ran and Step3 not in last antiSpamStep3Hours.
            for (var i = 0; i < scenario.Steps.Count; i++)
            {
                var stepPlans = executionPlans
                    .Where(x => x.StepIndex == i)
                    .ToList();
                if (stepPlans.Count == 0)
                {
                    continue;
                }

                var stepLastRun = stepPlans
                    .Where(x => x.Campaign.LastRunAtUtc.HasValue)
                    .Select(x => x.Campaign.LastRunAtUtc!.Value)
                    .OrderByDescending(x => x)
                    .FirstOrDefault();
                var hasStepLastRun = stepPlans.Any(x => x.Campaign.LastRunAtUtc.HasValue);
                DateTime? lastRun = hasStepLastRun ? stepLastRun : null;
                var stepCooldownHours = i switch
                {
                    0 => antiSpamStep1Hours,
                    1 => antiSpamStep2Hours,
                    _ => antiSpamStep3Hours
                };

                var canRun = i switch
                {
                    0 => !lastRun.HasValue || (now - lastRun.Value).TotalHours >= stepCooldownHours,
                    1 => executionPlans.Any(x => x.StepIndex == 0 && x.Campaign.LastRunAtUtc.HasValue) &&
                         (!lastRun.HasValue || (now - lastRun.Value).TotalHours >= stepCooldownHours),
                    _ => executionPlans.Any(x => x.StepIndex == 1 && x.Campaign.LastRunAtUtc.HasValue) &&
                         (!lastRun.HasValue || (now - lastRun.Value).TotalHours >= stepCooldownHours)
                };

                if (!canRun)
                {
                    continue;
                }

                foreach (var plan in stepPlans)
                {
                    var scenarioNotificationType = BuildScenarioNotificationType(
                        scenario.Key,
                        plan.StepCode,
                        splitChannelsToCampaigns ? plan.ChannelKey : null);
                    var audienceUsers = await ResolveCampaignAudienceUsersAsync(plan.Campaign, now, ct);
                    var stepWindowFromUtc = now.AddHours(-stepCooldownHours);
                    var scenarioWindowFromUtc = now.AddDays(-scenarioWindowDays);

                    var audienceRecipients = audienceUsers
                        .Select(x => x.Recipient)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var recentStepRecipients = audienceRecipients.Count == 0
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : (await _db.TransactionalNotificationLogs.AsNoTracking()
                            .Where(x =>
                                x.Status == "Sent" &&
                                x.NotificationType == scenarioNotificationType &&
                                x.AttemptedAtUtc >= stepWindowFromUtc &&
                                audienceRecipients.Contains(x.Recipient))
                            .Select(x => x.Recipient)
                            .Distinct()
                            .ToListAsync(ct))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var recentScenarioCounts = audienceRecipients.Count == 0
                        ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        : (await _db.TransactionalNotificationLogs.AsNoTracking()
                            .Where(x =>
                                x.Status == "Sent" &&
                                x.NotificationType.StartsWith($"marketing_scenario:{scenario.Key}:") &&
                                x.AttemptedAtUtc >= scenarioWindowFromUtc &&
                                audienceRecipients.Contains(x.Recipient))
                            .GroupBy(x => x.Recipient)
                            .Select(g => new { Recipient = g.Key, Count = g.Count() })
                            .ToListAsync(ct))
                        .ToDictionary(x => x.Recipient, x => x.Count, StringComparer.OrdinalIgnoreCase);

                    if (!currentRunScenarioTouchCounts.TryGetValue(scenario.Key, out var currentScenarioCounts))
                    {
                        currentScenarioCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        currentRunScenarioTouchCounts[scenario.Key] = currentScenarioCounts;
                    }

                    if (!currentRunStepTouches.TryGetValue(scenarioNotificationType, out var currentStepRecipients))
                    {
                        currentStepRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        currentRunStepTouches[scenarioNotificationType] = currentStepRecipients;
                    }

                    var allowedUsers = new List<CampaignAudienceUser>(audienceUsers.Count);
                    foreach (var audienceUser in audienceUsers)
                    {
                        var recipient = audienceUser.Recipient;
                        var stepAlreadyTouched = recentStepRecipients.Contains(recipient) || currentStepRecipients.Contains(recipient);
                        if (stepAlreadyTouched)
                        {
                            continue;
                        }

                        var totalTouchesInWindow = recentScenarioCounts.GetValueOrDefault(recipient) +
                                                   currentScenarioCounts.GetValueOrDefault(recipient);
                        if (totalTouchesInWindow >= scenarioMaxTouchesPerUser)
                        {
                            continue;
                        }

                        allowedUsers.Add(audienceUser);
                    }

                    var skippedByAntiSpam = Math.Max(0, audienceUsers.Count - allowedUsers.Count);
                    antiSpamSkippedUsers += skippedByAntiSpam;

                    var notes = skippedByAntiSpam > 0
                        ? $"Scenario anti-spam skipped {skippedByAntiSpam} users."
                        : null;

                    var run = await ExecuteCampaignRunAsync(
                        plan.Campaign,
                        $"Scenario:{scenario.Key}:{plan.StepCode}:{plan.ChannelKey}",
                        now,
                        attributionWindowDays,
                        ct,
                        audienceUsers: allowedUsers,
                        notificationType: scenarioNotificationType,
                        runNotes: notes);

                    if (run.SentCount > 0 && allowedUsers.Count > 0)
                    {
                        foreach (var recipient in allowedUsers.Select(x => x.Recipient).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            currentStepRecipients.Add(recipient);
                            currentScenarioCounts[recipient] = currentScenarioCounts.GetValueOrDefault(recipient) + 1;
                        }
                    }

                    executedRuns++;
                    totalTargetUsers += run.TargetUsers;
                    totalSent += run.SentCount;
                    totalFailed += run.FailedCount;
                    totalRevenue += run.RevenueAttributed;
                    totalCost += run.DiscountCost;

                    stepsExecuted.Add(new MarketingScenarioStepExecutionDto(
                        scenario.Key,
                        plan.StepCode,
                        plan.Campaign.Id,
                        plan.Campaign.Name,
                        run.Id,
                        run.TargetUsers,
                        run.SentCount,
                        run.RevenueAttributed,
                        run.DiscountCost,
                        skippedByAntiSpam,
                        run.StartedAtUtc));
                }
            }
        }

        if (!dryRun)
        {
            await _db.SaveChangesAsync(ct);
        }

        var roiPercent = totalCost > 0m
            ? decimal.Round((totalRevenue - totalCost) * 100m / totalCost, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return Ok(new MarketingScenarioAutomationResultDto(
            dryRun,
            scenarios.Count,
            ensuredCampaigns,
            executedRuns,
            totalTargetUsers,
            totalSent,
            totalFailed,
            totalRevenue,
            totalCost,
            roiPercent,
            antiSpamSkippedUsers,
            stepsExecuted,
            now));
    }

    private async Task<List<MarketingCampaignRoiDto>> BuildCampaignRoiRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? q,
        string? normalizedStatus,
        Guid? segmentId,
        string? normalizedChannel,
        int? minSent,
        decimal? minRoi,
        CancellationToken ct)
    {
        var rows = await BuildCampaignAnalyticsRowsAsync(
            fromUtc,
            toUtc,
            q,
            normalizedStatus,
            segmentId,
            normalizedChannel,
            minSent,
            minRoi,
            ct);

        return rows.Select(x => new MarketingCampaignRoiDto(
            x.CampaignId,
            x.CampaignName,
            x.Status,
            x.RunsCount,
            x.TargetUsers,
            x.SentCount,
            x.FailedCount,
            x.RevenueAttributed,
            x.DiscountCost,
            x.RoiPercent,
            x.LastRunAtUtc,
            x.SegmentId,
            x.SegmentName,
            BuildCampaignChannels(x.ChannelEmail, x.ChannelPush, x.ChannelInApp),
            x.StartAtUtc,
            x.EndAtUtc))
            .ToList();
    }

    private async Task<List<MarketingCampaignTimeSeriesPointDto>> BuildCampaignTimeSeriesPointsAsync(
        IReadOnlyList<CampaignAnalyticsRow> campaignRows,
        DateTime fromUtc,
        DateTime toUtc,
        string granularity,
        CancellationToken ct)
    {
        var campaignIds = campaignRows.Select(x => x.CampaignId).ToList();
        if (campaignIds.Count == 0)
        {
            return new List<MarketingCampaignTimeSeriesPointDto>();
        }

        var runs = await _db.MarketingCampaignRuns.AsNoTracking()
            .Where(x =>
                campaignIds.Contains(x.CampaignId) &&
                x.StartedAtUtc >= fromUtc &&
                x.StartedAtUtc < toUtc)
            .Select(x => new
            {
                x.StartedAtUtc,
                x.SentCount,
                x.RevenueAttributed,
                x.DiscountCost
            })
            .ToListAsync(ct);

        return runs
            .GroupBy(x => ResolveTimeSeriesBucketStartUtc(x.StartedAtUtc, granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var sent = g.Sum(x => x.SentCount);
                var revenue = g.Sum(x => x.RevenueAttributed);
                var cost = g.Sum(x => x.DiscountCost);
                var roiPercent = cost > 0m
                    ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new MarketingCampaignTimeSeriesPointDto(
                    g.Key,
                    g.Count(),
                    sent,
                    revenue,
                    cost,
                    roiPercent);
            })
            .ToList();
    }

    private async Task<List<MarketingCampaignExperimentDto>> BuildCampaignExperimentRowsAsync(
        IReadOnlyList<CampaignAnalyticsRow> campaignRows,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct)
    {
        var campaignIds = campaignRows.Select(x => x.CampaignId).Distinct().ToList();
        if (campaignIds.Count == 0)
        {
            return new List<MarketingCampaignExperimentDto>();
        }

        var runs = await _db.MarketingCampaignRuns.AsNoTracking()
            .Where(x =>
                campaignIds.Contains(x.CampaignId) &&
                x.StartedAtUtc >= fromUtc &&
                x.StartedAtUtc < toUtc)
            .Select(x => new
            {
                x.CampaignId,
                x.RunType,
                x.TargetUsers,
                x.SentCount,
                x.FailedCount,
                x.RevenueAttributed,
                x.DiscountCost,
                x.StartedAtUtc
            })
            .ToListAsync(ct);

        return runs
            .GroupBy(x => NormalizeExperimentBucket(x.RunType))
            .Select(g =>
            {
                var campaignsCount = g.Select(x => x.CampaignId).Distinct().Count();
                var targetUsers = g.Sum(x => x.TargetUsers);
                var sent = g.Sum(x => x.SentCount);
                var failed = g.Sum(x => x.FailedCount);
                var revenue = g.Sum(x => x.RevenueAttributed);
                var cost = g.Sum(x => x.DiscountCost);
                var roiPercent = cost > 0m
                    ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var deliveryRate = (sent + failed) > 0
                    ? decimal.Round((decimal)sent * 100m / (sent + failed), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var avgRevenuePerSent = sent > 0
                    ? decimal.Round(revenue / sent, 4, MidpointRounding.AwayFromZero)
                    : 0m;

                return new MarketingCampaignExperimentDto(
                    g.Key,
                    campaignsCount,
                    g.Count(),
                    targetUsers,
                    sent,
                    failed,
                    revenue,
                    cost,
                    roiPercent,
                    deliveryRate,
                    avgRevenuePerSent,
                    g.Min(x => x.StartedAtUtc),
                    g.Max(x => x.StartedAtUtc));
            })
            .OrderByDescending(x => x.SentCount)
            .ThenByDescending(x => x.RoiPercent)
            .ThenBy(x => x.Experiment)
            .ToList();
    }

    private static int ExperimentPriority(string experiment)
    {
        if (string.Equals(experiment, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(experiment, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(experiment, "Scenario", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 10;
    }

    private static string ResolveUpliftConfidence(int baselineSent, int variantSent)
    {
        var minSent = Math.Min(baselineSent, variantSent);
        return minSent switch
        {
            >= 2000 => "High",
            >= 500 => "Medium",
            _ => "Low"
        };
    }

    private static List<MarketingActionRecommendationDto> BuildExperimentRecommendations(
        MarketingCampaignExperimentDto baseline,
        MarketingCampaignExperimentDto variant,
        decimal upliftRoiPct,
        decimal upliftRevenuePerSentPct,
        decimal deliveryDeltaPct,
        string confidence)
    {
        var list = new List<MarketingActionRecommendationDto>();

        if (string.Equals(confidence, "Low", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new MarketingActionRecommendationDto(
                "High",
                "Increase sample size before full rollout",
                "Confidence is low because one experiment arm has limited sent volume.",
                "Keep split for 7 days or until each arm reaches at least 500 sent."));
        }

        if (upliftRoiPct >= 8m && deliveryDeltaPct >= -1m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "High",
                $"Scale {variant.Experiment} as primary strategy",
                $"{variant.Experiment} outperforms {baseline.Experiment} by {upliftRoiPct:0.##} ROI points.",
                "Increase traffic share to winner by +20% and monitor for 48h."));
        }
        else if (upliftRoiPct <= -8m || deliveryDeltaPct <= -3m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "High",
                $"Reduce exposure of {variant.Experiment}",
                $"Variant underperforms baseline (ROI delta {upliftRoiPct:0.##} pts, delivery delta {deliveryDeltaPct:0.##} pts).",
                "Rollback to baseline and audit copy/channel configuration."));
        }
        else if (Math.Abs(upliftRoiPct) < 3m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "Medium",
                "Continue A/B test",
                "Uplift is not yet materially different between baseline and variant.",
                "Run one more cycle and reassess with updated volume."));
        }

        if (upliftRevenuePerSentPct >= 5m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "Medium",
                "Prioritize high-value audiences for winner",
                $"Revenue per sent uplift is {upliftRevenuePerSentPct:0.##}%.",
                "Clone winning campaign for top RFM segments first."));
        }
        else if (upliftRevenuePerSentPct <= -5m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "Medium",
                "Review offer economics",
                $"Revenue per sent is down by {Math.Abs(upliftRevenuePerSentPct):0.##}%.",
                "Reduce discount depth or retarget to better-converting cohorts."));
        }

        if (deliveryDeltaPct < 0m)
        {
            list.Add(new MarketingActionRecommendationDto(
                "Medium",
                "Investigate deliverability drift",
                $"Delivery dropped by {Math.Abs(deliveryDeltaPct):0.##} points for the variant arm.",
                "Check provider logs and retry/failure reasons by channel."));
        }

        if (list.Count == 0)
        {
            list.Add(new MarketingActionRecommendationDto(
                "Low",
                "Keep current setup",
                "No major negative signal detected in current experiment window.",
                "Maintain cadence and review again after next automation run."));
        }

        return list;
    }

    private async Task<List<CampaignAnalyticsRow>> BuildCampaignAnalyticsRowsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? q,
        string? normalizedStatus,
        Guid? segmentId,
        string? normalizedChannel,
        int? minSent,
        decimal? minRoi,
        CancellationToken ct)
    {
        var campaignsQuery = _db.MarketingCampaigns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            campaignsQuery = campaignsQuery.Where(x => x.Name.Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            campaignsQuery = campaignsQuery.Where(x => x.Status == normalizedStatus);
        }

        if (segmentId.HasValue && segmentId.Value != Guid.Empty)
        {
            campaignsQuery = campaignsQuery.Where(x => x.SegmentId == segmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedChannel))
        {
            campaignsQuery = normalizedChannel switch
            {
                "email" => campaignsQuery.Where(x => x.ChannelEmail),
                "push" => campaignsQuery.Where(x => x.ChannelPush),
                "inapp" => campaignsQuery.Where(x => x.ChannelInApp),
                "multi" => campaignsQuery.Where(x =>
                    (x.ChannelEmail ? 1 : 0) +
                    (x.ChannelPush ? 1 : 0) +
                    (x.ChannelInApp ? 1 : 0) >= 2),
                _ => campaignsQuery
            };
        }

        var campaigns = await campaignsQuery
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Status,
                x.SegmentId,
                SegmentName = x.Segment != null ? x.Segment.Name : null,
                x.ChannelEmail,
                x.ChannelPush,
                x.ChannelInApp,
                x.StartAtUtc,
                x.EndAtUtc,
                x.LastRunAtUtc
            })
            .ToListAsync(ct);

        var campaignIds = campaigns.Select(x => x.Id).ToList();
        var runs = campaignIds.Count == 0
            ? new List<(Guid CampaignId, int TargetUsers, int SentCount, int FailedCount, decimal RevenueAttributed, decimal DiscountCost)>()
            : (await _db.MarketingCampaignRuns.AsNoTracking()
                .Where(x =>
                    campaignIds.Contains(x.CampaignId) &&
                    x.StartedAtUtc >= fromUtc &&
                    x.StartedAtUtc < toUtc)
                .Select(x => new
                {
                    x.CampaignId,
                    x.TargetUsers,
                    x.SentCount,
                    x.FailedCount,
                    x.RevenueAttributed,
                    x.DiscountCost
                })
                .ToListAsync(ct))
            .Select(x => (x.CampaignId, x.TargetUsers, x.SentCount, x.FailedCount, x.RevenueAttributed, x.DiscountCost))
            .ToList();

        var grouped = runs
            .GroupBy(x => x.CampaignId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    RunsCount = g.Count(),
                    TargetUsers = g.Sum(x => x.TargetUsers),
                    SentCount = g.Sum(x => x.SentCount),
                    FailedCount = g.Sum(x => x.FailedCount),
                    Revenue = g.Sum(x => x.RevenueAttributed),
                    Cost = g.Sum(x => x.DiscountCost)
                });

        var rows = campaigns.Select(c =>
        {
            var has = grouped.TryGetValue(c.Id, out var agg);
            var revenue = has ? agg!.Revenue : 0m;
            var cost = has ? agg!.Cost : 0m;
            var roiPercent = cost > 0m
                ? decimal.Round((revenue - cost) * 100m / cost, 2, MidpointRounding.AwayFromZero)
                : 0m;

            return new CampaignAnalyticsRow(
                c.Id,
                c.Name,
                c.Status,
                c.SegmentId,
                c.SegmentName,
                c.ChannelEmail,
                c.ChannelPush,
                c.ChannelInApp,
                c.StartAtUtc,
                c.EndAtUtc,
                c.LastRunAtUtc,
                has ? agg!.RunsCount : 0,
                has ? agg!.TargetUsers : 0,
                has ? agg!.SentCount : 0,
                has ? agg!.FailedCount : 0,
                revenue,
                cost,
                roiPercent);
        }).ToList();

        if (minSent.HasValue)
        {
            var minSentValue = Math.Max(0, minSent.Value);
            rows = rows.Where(x => x.SentCount >= minSentValue).ToList();
        }

        if (minRoi.HasValue)
        {
            rows = rows.Where(x => x.RoiPercent >= minRoi.Value).ToList();
        }

        return rows;
    }

    private async Task<(int created, int updated, int total)> UpsertRfmSystemSegmentsAsync(
        DateTime now,
        CancellationToken ct)
    {
        var templates = GetRfmSegmentTemplates();

        var existing = await _db.MarketingSegments
            .Where(x => x.IsSystem && x.Name.StartsWith("RFM - "))
            .ToListAsync(ct);

        var created = 0;
        var updated = 0;

        foreach (var template in templates)
        {
            var entity = existing.FirstOrDefault(x => x.Name == template.Name);
            var criteriaJson = $"{{\"rfmTag\":\"{template.Tag}\"}}";

            if (entity is null)
            {
                entity = new MarketingSegment
                {
                    Name = template.Name,
                    Description = template.Description,
                    CriteriaJson = criteriaJson,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                _db.MarketingSegments.Add(entity);
                created++;
            }
            else
            {
                entity.Description = template.Description;
                entity.CriteriaJson = criteriaJson;
                entity.IsSystem = true;
                entity.IsActive = true;
                entity.UpdatedAtUtc = now;
                updated++;
            }
        }

        return (created, updated, templates.Length);
    }

    private static RfmSegmentTemplate[] GetRfmSegmentTemplates() =>
    [
        new RfmSegmentTemplate("RFM - Champions", "Clients recents, frequents, forte valeur", "champions"),
        new RfmSegmentTemplate("RFM - Loyal", "Clients reguliers avec valeur stable", "loyal"),
        new RfmSegmentTemplate("RFM - Potential", "Clients recents avec faible frequence", "potential"),
        new RfmSegmentTemplate("RFM - At Risk", "Clients historiques sans commande recente", "at_risk"),
        new RfmSegmentTemplate("RFM - Hibernating", "Clients inactifs et faible engagement", "hibernating"),
        new RfmSegmentTemplate("RFM - New", "Nouveaux clients recents", "new")
    ];

    private static string ClassifyRfmTag(int daysSinceLastOrder, int frequency180, decimal monetary180, int paidCountAll)
    {
        if (paidCountAll <= 0)
        {
            return "hibernating";
        }

        if (daysSinceLastOrder <= 30 && paidCountAll <= 1)
        {
            return "new";
        }

        if (daysSinceLastOrder <= 30 && frequency180 >= 5 && monetary180 >= 200m)
        {
            return "champions";
        }

        if (daysSinceLastOrder <= 60 && frequency180 >= 3 && monetary180 >= 120m)
        {
            return "loyal";
        }

        if (daysSinceLastOrder <= 30 && frequency180 <= 2)
        {
            return "potential";
        }

        if (daysSinceLastOrder > 90 && paidCountAll >= 3)
        {
            return "at_risk";
        }

        if (daysSinceLastOrder > 120 && paidCountAll <= 2)
        {
            return "hibernating";
        }

        return "loyal";
    }

    private async Task<MarketingCampaignRun> ExecuteCampaignRunAsync(
        MarketingCampaign campaign,
        string runType,
        DateTime now,
        int attributionWindowDays,
        CancellationToken ct,
        IReadOnlyList<CampaignAudienceUser>? audienceUsers = null,
        string? notificationType = null,
        string? runNotes = null)
    {
        var targetUsers = audienceUsers?.Count ?? 0;
        if (audienceUsers is null)
        {
            var criteria = ParseCriteria(campaign.Segment?.CriteriaJson);
            var usersQuery = BuildCampaignUsersQuery(criteria, now);
            targetUsers = await usersQuery.CountAsync(ct);
        }

        var enabledChannelCount = (campaign.ChannelEmail ? 1 : 0) + (campaign.ChannelPush ? 1 : 0) + (campaign.ChannelInApp ? 1 : 0);
        var sentCount = enabledChannelCount > 0 ? targetUsers : 0;
        var failedCount = 0;

        var revenueAttributed = 0m;
        var discountCost = 0m;
        if (campaign.CouponId.HasValue)
        {
            var couponCode = campaign.Coupon?.Code;
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                couponCode = await _db.Coupons.AsNoTracking()
                    .Where(x => x.Id == campaign.CouponId.Value)
                    .Select(x => x.Code)
                    .FirstOrDefaultAsync(ct);
            }

            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var minWindowDays = Math.Clamp(attributionWindowDays, 1, 180);
                var fromUtc = campaign.LastRunAtUtc.HasValue && campaign.LastRunAtUtc.Value < now
                    ? campaign.LastRunAtUtc.Value
                    : now.AddDays(-minWindowDays);

                var attributedOrders = await _db.Orders.AsNoTracking()
                    .Where(o =>
                        o.PaymentStatus == "Paid" &&
                        o.CreatedAtUtc >= fromUtc &&
                        o.CreatedAtUtc <= now &&
                        o.PromoCode == couponCode)
                    .Select(o => new { o.TotalAmount, o.Discount })
                    .ToListAsync(ct);

                revenueAttributed = attributedOrders.Sum(o => o.TotalAmount);
                discountCost = attributedOrders.Sum(o => o.Discount);
            }
        }

        var run = new MarketingCampaignRun
        {
            CampaignId = campaign.Id,
            RunType = runType,
            Status = "Completed",
            TargetUsers = targetUsers,
            SentCount = sentCount,
            FailedCount = failedCount,
            RevenueAttributed = revenueAttributed,
            DiscountCost = discountCost,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            Notes = !string.IsNullOrWhiteSpace(runNotes)
                ? TrimToMax(runNotes!, 1000)
                : (targetUsers == 0
                    ? "No matching audience for current segment criteria."
                    : "Campaign executed using current segment criteria."),
            CreatedAtUtc = now
        };

        _db.MarketingCampaignRuns.Add(run);

        if (!string.IsNullOrWhiteSpace(notificationType) &&
            sentCount > 0 &&
            audienceUsers is { Count: > 0 })
        {
            var channel = BuildChannelLabel(campaign);
            var normalizedNotificationType = TrimToMax(notificationType!, 80);
            var subject = TrimToMax(campaign.MessageTitle ?? campaign.Name, 250);

            foreach (var recipient in audienceUsers
                         .Select(x => x.Recipient)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _db.TransactionalNotificationLogs.Add(new TransactionalNotificationLog
                {
                    NotificationType = normalizedNotificationType,
                    Channel = channel,
                    Recipient = TrimToMax(recipient, 180),
                    Subject = subject,
                    Status = "Sent",
                    AttemptedAtUtc = now,
                    CreatedAtUtc = now
                });
            }
        }

        campaign.LastRunAtUtc = now;
        if (campaign.Status == CampaignStatusDraft)
        {
            campaign.Status = CampaignStatusActive;
        }

        if (campaign.EndAtUtc.HasValue && campaign.EndAtUtc.Value <= now)
        {
            campaign.Status = CampaignStatusCompleted;
        }

        campaign.UpdatedAtUtc = now;

        return run;
    }

    private async Task<List<CampaignAudienceUser>> ResolveCampaignAudienceUsersAsync(
        MarketingCampaign campaign,
        DateTime now,
        CancellationToken ct)
    {
        var criteria = ParseCriteria(campaign.Segment?.CriteriaJson);
        var raw = await BuildCampaignUsersQuery(criteria, now)
            .Select(x => new { x.Id, x.Email })
            .ToListAsync(ct);

        return raw
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .Select(x => new CampaignAudienceUser(
                x.Id,
                NormalizeRecipient(x.Email!)))
            .GroupBy(x => x.Recipient, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string BuildScenarioNotificationType(string scenarioKey, string stepCode, string? channelKey = null)
    {
        var suffix = string.IsNullOrWhiteSpace(channelKey)
            ? string.Empty
            : $":{channelKey.Trim().ToLowerInvariant()}";
        return TrimToMax($"marketing_scenario:{scenarioKey}:{stepCode}{suffix}", 80);
    }

    private static IReadOnlyList<ScenarioChannelSpec> BuildScenarioChannelSpecs(ScenarioStep step, bool splitByChannel)
    {
        if (!splitByChannel)
        {
            return new[]
            {
                new ScenarioChannelSpec("all", "All", step.ChannelEmail, step.ChannelPush, step.ChannelInApp)
            };
        }

        var specs = new List<ScenarioChannelSpec>(3);
        if (step.ChannelEmail)
        {
            specs.Add(new ScenarioChannelSpec("email", "Email", true, false, false));
        }

        if (step.ChannelPush)
        {
            specs.Add(new ScenarioChannelSpec("push", "Push", false, true, false));
        }

        if (step.ChannelInApp)
        {
            specs.Add(new ScenarioChannelSpec("inapp", "InApp", false, false, true));
        }

        if (specs.Count == 0)
        {
            specs.Add(new ScenarioChannelSpec("none", "None", false, false, false));
        }

        return specs;
    }

    private static string BuildChannelLabel(MarketingCampaign campaign)
    {
        var channels = new List<string>(3);
        if (campaign.ChannelEmail) channels.Add("Email");
        if (campaign.ChannelPush) channels.Add("Push");
        if (campaign.ChannelInApp) channels.Add("InApp");

        if (channels.Count == 0)
        {
            channels.Add("None");
        }

        return TrimToMax(string.Join("+", channels), 30);
    }

    private static string NormalizeRecipient(string value)
        => value.Trim().ToLowerInvariant();

    private static string TrimToMax(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private IQueryable<User> BuildCampaignUsersQuery(SegmentCriteria criteria, DateTime now)
    {
        var usersQuery = _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Client && u.IsActive);
        var from180Utc = now.AddDays(-180);

        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            var city = criteria.City.Trim();
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.DeliveryCity == city));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Country))
        {
            var country = criteria.Country.Trim();
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.DeliveryCountry == country));
        }

        if (criteria.MinOrders.HasValue && criteria.MinOrders.Value > 0)
        {
            var minOrders = criteria.MinOrders.Value;
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid") >= minOrders);
        }

        if (criteria.LastOrderDays.HasValue && criteria.LastOrderDays.Value > 0)
        {
            var fromUtc = now.AddDays(-criteria.LastOrderDays.Value);
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Any(o =>
                    o.UserId == u.Id &&
                    o.PaymentStatus == "Paid" &&
                    o.CreatedAtUtc >= fromUtc));
        }

        if (criteria.MinLifetimeSpend.HasValue && criteria.MinLifetimeSpend.Value > 0m)
        {
            var minSpend = criteria.MinLifetimeSpend.Value;
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Where(o => o.UserId == u.Id && o.PaymentStatus == "Paid")
                    .Sum(o => (decimal?)o.TotalAmount) >= minSpend);
        }

        if (!string.IsNullOrWhiteSpace(criteria.RfmTag))
        {
            usersQuery = ApplyRfmTagFilter(usersQuery, criteria.RfmTag!, now, from180Utc);
        }

        if (criteria.RecencyDaysMax.HasValue && criteria.RecencyDaysMax.Value > 0)
        {
            var fromUtc = now.AddDays(-criteria.RecencyDaysMax.Value);
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Any(o =>
                    o.UserId == u.Id &&
                    o.PaymentStatus == "Paid" &&
                    o.CreatedAtUtc >= fromUtc));
        }

        if (criteria.FrequencyMin180d.HasValue && criteria.FrequencyMin180d.Value > 0)
        {
            var minFrequency = criteria.FrequencyMin180d.Value;
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Count(o =>
                    o.UserId == u.Id &&
                    o.PaymentStatus == "Paid" &&
                    o.CreatedAtUtc >= from180Utc) >= minFrequency);
        }

        if (criteria.MonetaryMin180d.HasValue && criteria.MonetaryMin180d.Value > 0m)
        {
            var minMonetary = criteria.MonetaryMin180d.Value;
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Where(o =>
                        o.UserId == u.Id &&
                        o.PaymentStatus == "Paid" &&
                        o.CreatedAtUtc >= from180Utc)
                    .Sum(o => (decimal?)o.TotalAmount) >= minMonetary);
        }

        if (criteria.DaysSinceLastOrderMin.HasValue && criteria.DaysSinceLastOrderMin.Value > 0)
        {
            var upperBound = now.AddDays(-criteria.DaysSinceLastOrderMin.Value);
            usersQuery = usersQuery.Where(u =>
                !_db.Orders.Any(o =>
                    o.UserId == u.Id &&
                    o.PaymentStatus == "Paid" &&
                    o.CreatedAtUtc > upperBound));
        }

        if (criteria.DaysSinceLastOrderMax.HasValue && criteria.DaysSinceLastOrderMax.Value > 0)
        {
            var lowerBound = now.AddDays(-criteria.DaysSinceLastOrderMax.Value);
            usersQuery = usersQuery.Where(u =>
                _db.Orders.Any(o =>
                    o.UserId == u.Id &&
                    o.PaymentStatus == "Paid" &&
                    o.CreatedAtUtc >= lowerBound));
        }

        return usersQuery;
    }

    private IQueryable<User> ApplyRfmTagFilter(
        IQueryable<User> usersQuery,
        string tag,
        DateTime now,
        DateTime from180Utc)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        var from30Utc = now.AddDays(-30);
        var from60Utc = now.AddDays(-60);
        var from90Utc = now.AddDays(-90);
        var from120Utc = now.AddDays(-120);

        return normalized switch
        {
            "champions" => usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from30Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc) >= 5 &&
                _db.Orders.Where(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc)
                    .Sum(o => (decimal?)o.TotalAmount) >= 200m),

            "loyal" => usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from60Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc) >= 3 &&
                _db.Orders.Where(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc)
                    .Sum(o => (decimal?)o.TotalAmount) >= 120m),

            "potential" => usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from30Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc) >= 1 &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from180Utc) <= 2),

            "at_risk" => usersQuery.Where(u =>
                !_db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from90Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid") >= 3),

            "hibernating" => usersQuery.Where(u =>
                !_db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from120Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid") <= 2),

            "new" => usersQuery.Where(u =>
                _db.Orders.Any(o => o.UserId == u.Id && o.PaymentStatus == "Paid" && o.CreatedAtUtc >= from30Utc) &&
                _db.Orders.Count(o => o.UserId == u.Id && o.PaymentStatus == "Paid") <= 1),

            _ => usersQuery
        };
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

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static MarketingBannerDto ToDto(MarketingBanner b) =>
        new(
            b.Id,
            b.Title,
            b.Subtitle,
            b.ImageUrl,
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

    private static IQueryable<MarketingBanner> ApplyStatusFilter(IQueryable<MarketingBanner> query, string? status, DateTime nowUtc)
    {
        var normalized = NormalizeStatus(status);
        if (normalized is null)
        {
            return query;
        }

        return normalized switch
        {
            BannerStatusLive => query.Where(b =>
                b.IsActive &&
                (!b.StartAtUtc.HasValue || b.StartAtUtc <= nowUtc) &&
                (!b.EndAtUtc.HasValue || b.EndAtUtc > nowUtc)),
            BannerStatusPlanned => query.Where(b =>
                b.IsActive &&
                b.StartAtUtc.HasValue &&
                b.StartAtUtc > nowUtc),
            BannerStatusExpired => query.Where(b =>
                b.IsActive &&
                b.EndAtUtc.HasValue &&
                b.EndAtUtc <= nowUtc),
            BannerStatusInactive => query.Where(b => !b.IsActive),
            _ => query
        };
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "live" => BannerStatusLive,
            "planned" => BannerStatusPlanned,
            "expired" => BannerStatusExpired,
            "inactive" => BannerStatusInactive,
            _ => null
        };
    }

    private static string ResolveStatus(bool isActive, DateTime? startAtUtc, DateTime? endAtUtc, DateTime nowUtc)
    {
        if (!isActive)
        {
            return BannerStatusInactive;
        }

        if (startAtUtc.HasValue && startAtUtc.Value > nowUtc)
        {
            return BannerStatusPlanned;
        }

        if (endAtUtc.HasValue && endAtUtc.Value <= nowUtc)
        {
            return BannerStatusExpired;
        }

        return BannerStatusLive;
    }

    private static bool IsValidPeriod(DateTime? startAtUtc, DateTime? endAtUtc)
        => !startAtUtc.HasValue || !endAtUtc.HasValue || startAtUtc <= endAtUtc;

    private static decimal ComputeCtr(int impressions, int clicks)
    {
        if (impressions <= 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)clicks * 100m / impressions, 6, MidpointRounding.AwayFromZero);
    }

    private static bool IsValidOptionalHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCampaignChannels(bool channelEmail, bool channelPush, bool channelInApp)
    {
        var channels = new List<string>(3);
        if (channelEmail) channels.Add("Email");
        if (channelPush) channels.Add("Push");
        if (channelInApp) channels.Add("InApp");
        return channels.Count == 0 ? "None" : string.Join("+", channels);
    }

    private static string NormalizeExperimentBucket(string? runType)
    {
        if (string.IsNullOrWhiteSpace(runType))
        {
            return "Unknown";
        }

        var normalized = runType.Trim();
        if (normalized.StartsWith("Scenario:", StringComparison.OrdinalIgnoreCase))
        {
            return "Scenario";
        }

        if (string.Equals(normalized, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return "Scheduled";
        }

        if (string.Equals(normalized, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            return "Manual";
        }

        return normalized;
    }

    private static string? NormalizeCampaignChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        return channel.Trim().ToLowerInvariant() switch
        {
            "email" => "email",
            "push" => "push",
            "inapp" => "inapp",
            "multi" => "multi",
            _ => null
        };
    }

    private static string? NormalizeGranularity(string? granularity)
    {
        if (string.IsNullOrWhiteSpace(granularity))
        {
            return null;
        }

        return granularity.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "week" => "week",
            _ => null
        };
    }

    private static DateTime ResolveTimeSeriesBucketStartUtc(DateTime timestampUtc, string granularity)
    {
        var dayStart = DateTime.SpecifyKind(timestampUtc.Date, DateTimeKind.Utc);
        if (!string.Equals(granularity, "week", StringComparison.OrdinalIgnoreCase))
        {
            return dayStart;
        }

        var offsetFromMonday = ((int)dayStart.DayOfWeek + 6) % 7;
        return dayStart.AddDays(-offsetFromMonday);
    }

    private static string? NormalizeCampaignStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "draft" => CampaignStatusDraft,
            "active" => CampaignStatusActive,
            "paused" => CampaignStatusPaused,
            "completed" => CampaignStatusCompleted,
            _ => null
        };
    }

    private static bool TryNormalizeJson(string raw, out string normalized)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            normalized = doc.RootElement.GetRawText();
            return true;
        }
        catch
        {
            normalized = "{}";
            return false;
        }
    }

    private static SegmentCriteria ParseCriteria(string? criteriaJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaJson))
        {
            return new SegmentCriteria(null, null, null, null, null, null, null, null, null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(criteriaJson);
            var root = doc.RootElement;
            return new SegmentCriteria(
                root.TryGetProperty("city", out var city) ? city.GetString() : null,
                root.TryGetProperty("country", out var country) ? country.GetString() : null,
                root.TryGetProperty("minOrders", out var minOrders) && minOrders.TryGetInt32(out var minOrderValue) ? minOrderValue : null,
                root.TryGetProperty("lastOrderDays", out var lastDays) && lastDays.TryGetInt32(out var lastDaysValue) ? lastDaysValue : null,
                root.TryGetProperty("minLifetimeSpend", out var minSpend) && minSpend.TryGetDecimal(out var minSpendValue) ? minSpendValue : null,
                root.TryGetProperty("rfmTag", out var rfmTag) ? rfmTag.GetString() : null,
                root.TryGetProperty("recencyDaysMax", out var recencyMax) && recencyMax.TryGetInt32(out var recencyMaxValue) ? recencyMaxValue : null,
                root.TryGetProperty("frequencyMin180d", out var frequencyMin) && frequencyMin.TryGetInt32(out var frequencyMinValue) ? frequencyMinValue : null,
                root.TryGetProperty("monetaryMin180d", out var monetaryMin) && monetaryMin.TryGetDecimal(out var monetaryMinValue) ? monetaryMinValue : null,
                root.TryGetProperty("daysSinceLastOrderMin", out var daysMin) && daysMin.TryGetInt32(out var daysMinValue) ? daysMinValue : null,
                root.TryGetProperty("daysSinceLastOrderMax", out var daysMax) && daysMax.TryGetInt32(out var daysMaxValue) ? daysMaxValue : null);
        }
        catch
        {
            return new SegmentCriteria(null, null, null, null, null, null, null, null, null, null, null);
        }
    }

    public sealed record MarketingSummaryDto(
        int Total,
        int Live,
        int Planned,
        int Expired,
        int Inactive,
        int Impressions,
        int Clicks,
        decimal CtrPercent);

    public sealed record RunMarketingAutomationRequest(
        bool? DisableExpired,
        bool? AutoPrioritizeLiveByCtr,
        int? MinImpressionsForCtr);

    public sealed record MarketingAutomationResultDto(
        int DisabledExpired,
        int ReprioritizedLive,
        int MinImpressionsForCtr,
        DateTime ExecutedAtUtc);

    public sealed record MarketingSegmentDto(
        Guid Id,
        string Name,
        string? Description,
        string CriteriaJson,
        bool IsSystem,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    public sealed record UpsertMarketingSegmentRequest(
        Guid? Id,
        string Name,
        string? Description,
        string CriteriaJson,
        bool IsSystem,
        bool IsActive);

    public sealed record PreviewMarketingSegmentRequest(
        string CriteriaJson,
        int? SampleSize);

    public sealed record MarketingSegmentPreviewDto(
        int AudienceCount,
        string CriteriaJson,
        IReadOnlyList<MarketingSegmentPreviewUserDto> SampleUsers,
        DateTime GeneratedAtUtc);

    public sealed record MarketingSegmentPreviewUserDto(
        Guid UserId,
        string? Email,
        int PaidOrders,
        decimal LifetimeSpend,
        DateTime? LastPaidAtUtc,
        DateTime CreatedAtUtc);

    public sealed record MarketingCampaignDto(
        Guid Id,
        string Name,
        string Status,
        Guid? SegmentId,
        string? SegmentName,
        Guid? CouponId,
        string? CouponCode,
        bool ChannelEmail,
        bool ChannelPush,
        bool ChannelInApp,
        string? MessageTitle,
        string? MessageBody,
        DateTime? StartAtUtc,
        DateTime? EndAtUtc,
        DateTime? LastRunAtUtc,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    public sealed record UpsertMarketingCampaignRequest(
        Guid? Id,
        string Name,
        string? Status,
        Guid? SegmentId,
        Guid? CouponId,
        bool ChannelEmail,
        bool ChannelPush,
        bool ChannelInApp,
        string? MessageTitle,
        string? MessageBody,
        DateTime? StartAtUtc,
        DateTime? EndAtUtc);

    public sealed record RunMarketingCampaignRequest(string? RunType);

    public sealed record MarketingCampaignRunDto(
        Guid Id,
        Guid CampaignId,
        string RunType,
        string Status,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        string? Notes);

    public sealed record MarketingCampaignSummaryDto(
        int TotalCampaigns,
        int DraftCampaigns,
        int ActiveCampaigns,
        int PausedCampaigns,
        int CompletedCampaigns,
        int ActiveInWindow,
        int RunsCount,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        DateTime FromUtc,
        DateTime ToUtc);

    public sealed record MarketingCampaignRoiDto(
        Guid CampaignId,
        string CampaignName,
        string Status,
        int RunsCount,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        DateTime? LastRunAtUtc,
        Guid? SegmentId,
        string? SegmentName,
        string Channels,
        DateTime? StartAtUtc,
        DateTime? EndAtUtc);

    public sealed record MarketingCampaignCohortDto(
        Guid? SegmentId,
        string SegmentName,
        int CampaignCount,
        int RunsCount,
        int SentCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        decimal ShareSentPercent,
        decimal ShareRevenuePercent);

    public sealed record MarketingCampaignTimeSeriesDto(
        string Granularity,
        DateTime FromUtc,
        DateTime ToUtc,
        IReadOnlyList<MarketingCampaignTimeSeriesPointDto> Points);

    public sealed record MarketingCampaignTimeSeriesPointDto(
        DateTime PeriodStartUtc,
        int RunsCount,
        int SentCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent);

    public sealed record MarketingCampaignExperimentDto(
        string Experiment,
        int CampaignsCount,
        int RunsCount,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        decimal DeliveryRatePercent,
        decimal AvgRevenuePerSent,
        DateTime FirstRunAtUtc,
        DateTime LastRunAtUtc);

    public sealed record MarketingCampaignExperimentInsightsDto(
        DateTime FromUtc,
        DateTime ToUtc,
        bool HasPair,
        MarketingCampaignExperimentDto? Baseline,
        MarketingCampaignExperimentDto? Variant,
        decimal UpliftRoiPct,
        decimal UpliftRevenuePerSentPct,
        decimal DeliveryDeltaPct,
        string Confidence,
        string WinnerExperiment,
        IReadOnlyList<MarketingActionRecommendationDto> Recommendations,
        IReadOnlyList<MarketingCampaignExperimentDto> Experiments);

    public sealed record MarketingActionRecommendationDto(
        string Priority,
        string Action,
        string Rationale,
        string ExecutionHint);

    public sealed record RunCampaignBatchAutomationRequest(
        bool? DryRun,
        bool? IncludeDraft,
        bool? AutoCompleteExpired,
        int? MaxCampaigns,
        int? AttributionWindowDays,
        string? RunType);

    public sealed record MarketingCampaignBatchAutomationResultDto(
        int EligibleCampaigns,
        int ProcessedCampaigns,
        int CreatedRuns,
        int CompletedExpiredCampaigns,
        int TotalTargetUsers,
        int TotalSent,
        int TotalFailed,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        bool DryRun,
        DateTime ExecutedAtUtc);

    public sealed record MarketingRfmInsightsDto(
        DateTime GeneratedAtUtc,
        DateTime WindowFromUtc,
        int TotalActiveClients,
        int ClientsWithPaidOrder,
        int ClientsWithoutPaidOrder,
        IReadOnlyList<MarketingRfmTagStatDto> Tags,
        IReadOnlyList<MarketingRfmUserRowDto> Users);

    public sealed record MarketingRfmTagStatDto(
        string Tag,
        int Count,
        decimal SharePercent);

    public sealed record MarketingRfmUserRowDto(
        Guid UserId,
        string Tag,
        int? DaysSinceLastPaidOrder,
        int FrequencyWindow,
        decimal MonetaryWindow,
        DateTime? LastPaidAtUtc);

    public sealed record MarketingRfmBootstrapResultDto(
        int CreatedSegments,
        int UpdatedSegments,
        int TotalTemplates,
        DateTime ExecutedAtUtc);

    public sealed record RunMarketingScenarioAutomationRequest(
        bool? DryRun,
        bool? IncludeWelcome,
        bool? IncludeWinback,
        bool? IncludeChurnRisk,
        int? MaxScenarios,
        int? AttributionWindowDays,
        int? AntiSpamStep1Hours = null,
        int? AntiSpamStep2Hours = null,
        int? AntiSpamStep3Hours = null,
        int? ScenarioWindowDays = null,
        int? ScenarioMaxTouchesPerUser = null,
        bool? SplitChannelsToCampaigns = null);

    public sealed record MarketingScenarioAutomationResultDto(
        bool DryRun,
        int PlannedScenarios,
        int EnsuredCampaigns,
        int ExecutedRuns,
        int TotalTargetUsers,
        int TotalSent,
        int TotalFailed,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent,
        int AntiSpamSkippedUsers,
        IReadOnlyList<MarketingScenarioStepExecutionDto> StepsExecuted,
        DateTime ExecutedAtUtc);

    public sealed record MarketingScenarioStepExecutionDto(
        string ScenarioKey,
        string StepCode,
        Guid CampaignId,
        string CampaignName,
        Guid RunId,
        int TargetUsers,
        int SentCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        int AntiSpamSkipped,
        DateTime StartedAtUtc);

    private sealed record SegmentCriteria(
        string? City,
        string? Country,
        int? MinOrders,
        int? LastOrderDays,
        decimal? MinLifetimeSpend,
        string? RfmTag,
        int? RecencyDaysMax,
        int? FrequencyMin180d,
        decimal? MonetaryMin180d,
        int? DaysSinceLastOrderMin,
        int? DaysSinceLastOrderMax);

    private sealed record ScenarioPlan(
        string Key,
        string Prefix,
        string SegmentName,
        IReadOnlyList<ScenarioStep> Steps)
    {
        public string Label => Prefix;
    }

    private sealed record ScenarioStep(
        string Code,
        string MessageBody,
        string Status,
        bool ChannelEmail,
        bool ChannelPush,
        bool ChannelInApp);

    private sealed record ScenarioChannelSpec(
        string Code,
        string Label,
        bool ChannelEmail,
        bool ChannelPush,
        bool ChannelInApp);

    private sealed record ScenarioCampaignExecutionPlan(
        int StepIndex,
        string StepCode,
        string ChannelKey,
        MarketingCampaign Campaign);

    private sealed record CampaignAudienceUser(
        Guid UserId,
        string Recipient);

    private sealed record CampaignAnalyticsRow(
        Guid CampaignId,
        string CampaignName,
        string Status,
        Guid? SegmentId,
        string? SegmentName,
        bool ChannelEmail,
        bool ChannelPush,
        bool ChannelInApp,
        DateTime? StartAtUtc,
        DateTime? EndAtUtc,
        DateTime? LastRunAtUtc,
        int RunsCount,
        int TargetUsers,
        int SentCount,
        int FailedCount,
        decimal RevenueAttributed,
        decimal DiscountCost,
        decimal RoiPercent);

    private sealed record PaidOrderPoint(
        Guid UserId,
        DateTime CreatedAtUtc,
        decimal TotalAmount);

    private sealed record RfmSegmentTemplate(
        string Name,
        string Description,
        string Tag);
}
