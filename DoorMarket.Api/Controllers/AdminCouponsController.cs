using DoorMarket.Application.Common;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/coupons")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminCouponsController : ControllerBase
{
    private const string DiscountTypePercent = "Percent";
    private const string DiscountTypeFixed = "Fixed";
    private const string ScopeGlobal = "Global";
    private const string ScopeShop = "Shop";
    private const string ScopeCategory = "Category";
    private const string ScopeCity = "City";
    private const string ScopeCountry = "Country";

    private readonly DoorMarketDbContext _db;

    public AdminCouponsController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CouponRowDto>>> GetCoupons(
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        [FromQuery] string? scope = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var normalizedScope = NormalizeScopeType(scope);
        if (scope is not null && normalizedScope is null)
        {
            return BadRequest("Scope invalide.");
        }

        var query = _db.Coupons.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(c =>
                c.Code.Contains(search) ||
                (c.Name != null && c.Name.Contains(search)));
        }

        if (active.HasValue)
        {
            query = query.Where(c => c.IsActive == active.Value);
        }

        if (normalizedScope is not null)
        {
            query = query.Where(c => c.ScopeType == normalizedScope);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var usageByCode = await BuildUsageStatsByCode(rows.Select(x => x.Code), ct);
        var now = DateTime.UtcNow;
        var items = rows
            .Select(c =>
            {
                usageByCode.TryGetValue(c.Code, out var usage);
                return new CouponRowDto(
                    c.Id,
                    c.Code,
                    c.Name,
                    c.IsActive,
                    ResolveOperationalStatus(c, usage, now),
                    c.DiscountType,
                    c.DiscountValue,
                    c.MaxDiscountAmount,
                    c.Currency,
                    c.MinSubtotal,
                    c.StartsAtUtc,
                    c.EndsAtUtc,
                    c.BudgetAmount,
                    c.UsageLimitTotal,
                    c.UsageLimitPerUser,
                    c.ScopeType,
                    c.ScopeShopId,
                    c.ScopeCategoryId,
                    c.ScopeCity,
                    c.ScopeCountry,
                    usage?.Uses ?? 0,
                    usage?.TotalDiscount ?? 0m,
                    usage?.LastUsedAtUtc,
                    c.CreatedAtUtc
                );
            })
            .ToList();

        return Ok(new PagedResult<CouponRowDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CouponDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null)
        {
            return NotFound();
        }

        var usage = await BuildUsageStatsByCode(new[] { coupon.Code }, ct);
        usage.TryGetValue(coupon.Code, out var stats);

        return Ok(ToDetail(coupon, stats));
    }

    [HttpPost]
    public async Task<ActionResult<CouponDetailDto>> Create([FromBody] UpsertCouponRequest req, CancellationToken ct = default)
    {
        var normalized = await NormalizeAndValidate(req, currentId: null, ct);
        if (!normalized.IsValid)
        {
            return BadRequest(normalized.Error);
        }

        var coupon = new Coupon
        {
            Code = normalized.Code!,
            Name = normalized.Name,
            Description = normalized.Description,
            IsActive = req.IsActive,
            DiscountType = normalized.DiscountType!,
            DiscountValue = normalized.DiscountValue,
            MaxDiscountAmount = normalized.MaxDiscountAmount,
            Currency = normalized.Currency,
            MinSubtotal = normalized.MinSubtotal,
            StartsAtUtc = normalized.StartsAtUtc,
            EndsAtUtc = normalized.EndsAtUtc,
            BudgetAmount = normalized.BudgetAmount,
            UsageLimitTotal = normalized.UsageLimitTotal,
            UsageLimitPerUser = normalized.UsageLimitPerUser,
            ScopeType = normalized.ScopeType!,
            ScopeShopId = normalized.ScopeShopId,
            ScopeCategoryId = normalized.ScopeCategoryId,
            ScopeCity = normalized.ScopeCity,
            ScopeCountry = normalized.ScopeCountry
        };

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(coupon, usage: null));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CouponDetailDto>> Update(Guid id, [FromBody] UpsertCouponRequest req, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null)
        {
            return NotFound();
        }

        var normalized = await NormalizeAndValidate(req, id, ct);
        if (!normalized.IsValid)
        {
            return BadRequest(normalized.Error);
        }

        coupon.Code = normalized.Code!;
        coupon.Name = normalized.Name;
        coupon.Description = normalized.Description;
        coupon.IsActive = req.IsActive;
        coupon.DiscountType = normalized.DiscountType!;
        coupon.DiscountValue = normalized.DiscountValue;
        coupon.MaxDiscountAmount = normalized.MaxDiscountAmount;
        coupon.Currency = normalized.Currency;
        coupon.MinSubtotal = normalized.MinSubtotal;
        coupon.StartsAtUtc = normalized.StartsAtUtc;
        coupon.EndsAtUtc = normalized.EndsAtUtc;
        coupon.BudgetAmount = normalized.BudgetAmount;
        coupon.UsageLimitTotal = normalized.UsageLimitTotal;
        coupon.UsageLimitPerUser = normalized.UsageLimitPerUser;
        coupon.ScopeType = normalized.ScopeType!;
        coupon.ScopeShopId = normalized.ScopeShopId;
        coupon.ScopeCategoryId = normalized.ScopeCategoryId;
        coupon.ScopeCity = normalized.ScopeCity;
        coupon.ScopeCountry = normalized.ScopeCountry;
        coupon.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var usage = await BuildUsageStatsByCode(new[] { coupon.Code }, ct);
        usage.TryGetValue(coupon.Code, out var stats);
        return Ok(ToDetail(coupon, stats));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<CouponDetailDto>> Activate(Guid id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null)
        {
            return NotFound();
        }

        coupon.IsActive = true;
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(coupon, usage: null));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<CouponDetailDto>> Deactivate(Guid id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null)
        {
            return NotFound();
        }

        coupon.IsActive = false;
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToDetail(coupon, usage: null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (coupon is null)
        {
            return NotFound();
        }

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Dictionary<string, CouponUsageStats>> BuildUsageStatsByCode(IEnumerable<string> rawCodes, CancellationToken ct)
    {
        var codes = rawCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count == 0)
        {
            return new Dictionary<string, CouponUsageStats>(StringComparer.OrdinalIgnoreCase);
        }

        var logs = await _db.PromoAuditLogs.AsNoTracking()
            .Where(x => x.Applied && x.Source == "Checkout" && x.PromoCode != null && codes.Contains(x.PromoCode))
            .ToListAsync(ct);

        return logs
            .GroupBy(x => x.PromoCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new CouponUsageStats(
                    g.Count(),
                    g.Sum(x => x.Discount),
                    g.Max(x => x.CreatedAtUtc)),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ValidationResult> NormalizeAndValidate(UpsertCouponRequest req, Guid? currentId, CancellationToken ct)
    {
        var code = NormalizeCode(req.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return ValidationResult.Fail("Code coupon requis.");
        }

        if (!IsValidCouponCode(code))
        {
            return ValidationResult.Fail("Code coupon invalide. Utiliser A-Z, 0-9, '-' ou '_'.");
        }

        var duplicateExists = await _db.Coupons.AsNoTracking()
            .AnyAsync(x => x.Code == code && (!currentId.HasValue || x.Id != currentId.Value), ct);
        if (duplicateExists)
        {
            return ValidationResult.Fail("Ce code coupon existe deja.");
        }

        var discountType = NormalizeDiscountType(req.DiscountType);
        if (discountType is null)
        {
            return ValidationResult.Fail("DiscountType invalide (Percent ou Fixed).");
        }

        if (req.DiscountValue <= 0m)
        {
            return ValidationResult.Fail("DiscountValue doit etre > 0.");
        }

        if (discountType == DiscountTypePercent && req.DiscountValue > 100m)
        {
            return ValidationResult.Fail("DiscountValue en pourcentage doit etre <= 100.");
        }

        if (req.MaxDiscountAmount.HasValue && req.MaxDiscountAmount.Value <= 0m)
        {
            return ValidationResult.Fail("MaxDiscountAmount doit etre > 0.");
        }

        if (req.MinSubtotal.HasValue && req.MinSubtotal.Value <= 0m)
        {
            return ValidationResult.Fail("MinSubtotal doit etre > 0.");
        }

        if (req.BudgetAmount.HasValue && req.BudgetAmount.Value <= 0m)
        {
            return ValidationResult.Fail("BudgetAmount doit etre > 0.");
        }

        if (req.UsageLimitTotal.HasValue && req.UsageLimitTotal.Value <= 0)
        {
            return ValidationResult.Fail("UsageLimitTotal doit etre > 0.");
        }

        if (req.UsageLimitPerUser.HasValue && req.UsageLimitPerUser.Value <= 0)
        {
            return ValidationResult.Fail("UsageLimitPerUser doit etre > 0.");
        }

        var startsAtUtc = req.StartsAtUtc?.ToUniversalTime();
        var endsAtUtc = req.EndsAtUtc?.ToUniversalTime();
        if (startsAtUtc.HasValue && endsAtUtc.HasValue && startsAtUtc.Value > endsAtUtc.Value)
        {
            return ValidationResult.Fail("La date de debut doit etre <= a la date de fin.");
        }

        string? currency = null;
        if (!string.IsNullOrWhiteSpace(req.Currency))
        {
            currency = req.Currency.Trim().ToUpperInvariant();
            if (currency.Length != 3)
            {
                return ValidationResult.Fail("Currency doit etre un code ISO 3 lettres.");
            }
        }

        var scopeType = NormalizeScopeType(req.ScopeType);
        if (scopeType is null)
        {
            return ValidationResult.Fail("ScopeType invalide (Global, Shop, Category, City, Country).");
        }

        Guid? scopeShopId = null;
        Guid? scopeCategoryId = null;
        string? scopeCity = null;
        string? scopeCountry = null;
        switch (scopeType)
        {
            case ScopeShop:
                if (!req.ScopeShopId.HasValue || req.ScopeShopId.Value == Guid.Empty)
                {
                    return ValidationResult.Fail("ScopeShopId requis pour ScopeType=Shop.");
                }

                var shopExists = await _db.Shops.AsNoTracking().AnyAsync(x => x.Id == req.ScopeShopId.Value, ct);
                if (!shopExists)
                {
                    return ValidationResult.Fail("ScopeShopId invalide.");
                }

                scopeShopId = req.ScopeShopId.Value;
                break;
            case ScopeCategory:
                if (!req.ScopeCategoryId.HasValue || req.ScopeCategoryId.Value == Guid.Empty)
                {
                    return ValidationResult.Fail("ScopeCategoryId requis pour ScopeType=Category.");
                }

                var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(x => x.Id == req.ScopeCategoryId.Value, ct);
                if (!categoryExists)
                {
                    return ValidationResult.Fail("ScopeCategoryId invalide.");
                }

                scopeCategoryId = req.ScopeCategoryId.Value;
                break;
            case ScopeCity:
                scopeCity = string.IsNullOrWhiteSpace(req.ScopeCity) ? null : req.ScopeCity.Trim();
                if (scopeCity is null)
                {
                    return ValidationResult.Fail("ScopeCity requis pour ScopeType=City.");
                }

                break;
            case ScopeCountry:
                scopeCountry = string.IsNullOrWhiteSpace(req.ScopeCountry) ? null : req.ScopeCountry.Trim();
                if (scopeCountry is null)
                {
                    return ValidationResult.Fail("ScopeCountry requis pour ScopeType=Country.");
                }

                break;
            case ScopeGlobal:
                break;
        }

        return ValidationResult.Ok(
            code,
            string.IsNullOrWhiteSpace(req.Name) ? null : req.Name.Trim(),
            string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            discountType,
            decimal.Round(req.DiscountValue, 2, MidpointRounding.AwayFromZero),
            req.MaxDiscountAmount.HasValue ? decimal.Round(req.MaxDiscountAmount.Value, 2, MidpointRounding.AwayFromZero) : null,
            currency,
            req.MinSubtotal.HasValue ? decimal.Round(req.MinSubtotal.Value, 2, MidpointRounding.AwayFromZero) : null,
            startsAtUtc,
            endsAtUtc,
            req.BudgetAmount.HasValue ? decimal.Round(req.BudgetAmount.Value, 2, MidpointRounding.AwayFromZero) : null,
            req.UsageLimitTotal,
            req.UsageLimitPerUser,
            scopeType,
            scopeShopId,
            scopeCategoryId,
            scopeCity,
            scopeCountry);
    }

    private static CouponDetailDto ToDetail(Coupon c, CouponUsageStats? usage)
    {
        var now = DateTime.UtcNow;
        return new CouponDetailDto(
            c.Id,
            c.Code,
            c.Name,
            c.Description,
            c.IsActive,
            ResolveOperationalStatus(c, usage, now),
            c.DiscountType,
            c.DiscountValue,
            c.MaxDiscountAmount,
            c.Currency,
            c.MinSubtotal,
            c.StartsAtUtc,
            c.EndsAtUtc,
            c.BudgetAmount,
            c.UsageLimitTotal,
            c.UsageLimitPerUser,
            c.ScopeType,
            c.ScopeShopId,
            c.ScopeCategoryId,
            c.ScopeCity,
            c.ScopeCountry,
            usage?.Uses ?? 0,
            usage?.TotalDiscount ?? 0m,
            usage?.LastUsedAtUtc,
            c.CreatedAtUtc,
            c.UpdatedAtUtc);
    }

    private static string ResolveOperationalStatus(Coupon c, CouponUsageStats? usage, DateTime nowUtc)
    {
        if (!c.IsActive)
        {
            return "Inactive";
        }

        if (c.StartsAtUtc.HasValue && nowUtc < c.StartsAtUtc.Value)
        {
            return "Planned";
        }

        if (c.EndsAtUtc.HasValue && nowUtc > c.EndsAtUtc.Value)
        {
            return "Expired";
        }

        var uses = usage?.Uses ?? 0;
        var totalDiscount = usage?.TotalDiscount ?? 0m;
        if ((c.UsageLimitTotal.HasValue && uses >= c.UsageLimitTotal.Value) ||
            (c.BudgetAmount.HasValue && totalDiscount >= c.BudgetAmount.Value))
        {
            return "Exhausted";
        }

        return "Live";
    }

    private static string? NormalizeDiscountType(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "percent" => DiscountTypePercent,
            "fixed" => DiscountTypeFixed,
            _ => null
        };

    private static string? NormalizeScopeType(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "global" => ScopeGlobal,
            "shop" => ScopeShop,
            "category" => ScopeCategory,
            "city" => ScopeCity,
            "country" => ScopeCountry,
            _ => null
        };

    private static string NormalizeCode(string? raw)
        => (raw ?? string.Empty).Trim().ToUpperInvariant();

    private static bool IsValidCouponCode(string code)
        => code.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    public sealed record UpsertCouponRequest(
        string Code,
        string? Name,
        string? Description,
        bool IsActive,
        string DiscountType,
        decimal DiscountValue,
        decimal? MaxDiscountAmount,
        string? Currency,
        decimal? MinSubtotal,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        decimal? BudgetAmount,
        int? UsageLimitTotal,
        int? UsageLimitPerUser,
        string ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        string? ScopeCity,
        string? ScopeCountry);

    public sealed record CouponRowDto(
        Guid Id,
        string Code,
        string? Name,
        bool IsActive,
        string OperationalStatus,
        string DiscountType,
        decimal DiscountValue,
        decimal? MaxDiscountAmount,
        string? Currency,
        decimal? MinSubtotal,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        decimal? BudgetAmount,
        int? UsageLimitTotal,
        int? UsageLimitPerUser,
        string ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        string? ScopeCity,
        string? ScopeCountry,
        int Uses,
        decimal UsedDiscountAmount,
        DateTime? LastUsedAtUtc,
        DateTime CreatedAtUtc);

    public sealed record CouponDetailDto(
        Guid Id,
        string Code,
        string? Name,
        string? Description,
        bool IsActive,
        string OperationalStatus,
        string DiscountType,
        decimal DiscountValue,
        decimal? MaxDiscountAmount,
        string? Currency,
        decimal? MinSubtotal,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        decimal? BudgetAmount,
        int? UsageLimitTotal,
        int? UsageLimitPerUser,
        string ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        string? ScopeCity,
        string? ScopeCountry,
        int Uses,
        decimal UsedDiscountAmount,
        DateTime? LastUsedAtUtc,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    private sealed record CouponUsageStats(int Uses, decimal TotalDiscount, DateTime? LastUsedAtUtc);

    private sealed record ValidationResult(
        bool IsValid,
        string? Error,
        string? Code,
        string? Name,
        string? Description,
        string? DiscountType,
        decimal DiscountValue,
        decimal? MaxDiscountAmount,
        string? Currency,
        decimal? MinSubtotal,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        decimal? BudgetAmount,
        int? UsageLimitTotal,
        int? UsageLimitPerUser,
        string? ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        string? ScopeCity,
        string? ScopeCountry)
    {
        public static ValidationResult Fail(string error)
            => new(false, error, null, null, null, null, 0m, null, null, null, null, null, null, null, null, null, null, null, null, null);

        public static ValidationResult Ok(
            string code,
            string? name,
            string? description,
            string discountType,
            decimal discountValue,
            decimal? maxDiscountAmount,
            string? currency,
            decimal? minSubtotal,
            DateTime? startsAtUtc,
            DateTime? endsAtUtc,
            decimal? budgetAmount,
            int? usageLimitTotal,
            int? usageLimitPerUser,
            string scopeType,
            Guid? scopeShopId,
            Guid? scopeCategoryId,
            string? scopeCity,
            string? scopeCountry)
            => new(true, null, code, name, description, discountType, discountValue, maxDiscountAmount, currency, minSubtotal, startsAtUtc, endsAtUtc, budgetAmount, usageLimitTotal, usageLimitPerUser, scopeType, scopeShopId, scopeCategoryId, scopeCity, scopeCountry);
    }
}
