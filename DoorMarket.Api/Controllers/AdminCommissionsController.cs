using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/commissions")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public sealed class AdminCommissionsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public AdminCommissionsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<CommissionRuleDto>>> GetRules(
        [FromQuery] bool? active = null,
        [FromQuery] string? scopeType = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var query = _db.CommissionRules.AsNoTracking().AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            if (!TryNormalizeScopeType(scopeType, out var normalizedScope))
            {
                return BadRequest("ScopeType invalide.");
            }

            query = query.Where(x => x.ScopeType == normalizedScope);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();
            query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        }

        var rows = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Priority)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ScopeType,
                x.ScopeShopId,
                x.ScopeCategoryId,
                x.ScopeProductId,
                x.Currency,
                x.PlatformFeeMode,
                x.PlatformFeeAmount,
                x.PlatformFeePercent,
                x.MinUnitPrice,
                x.MaxUnitPrice,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.Priority,
                x.IsActive,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        var shopIds = rows.Where(x => x.ScopeShopId.HasValue).Select(x => x.ScopeShopId!.Value).Distinct().ToList();
        var categoryIds = rows.Where(x => x.ScopeCategoryId.HasValue).Select(x => x.ScopeCategoryId!.Value).Distinct().ToList();
        var productIds = rows.Where(x => x.ScopeProductId.HasValue).Select(x => x.ScopeProductId!.Value).Distinct().ToList();

        var shopNames = await _db.Shops.AsNoTracking()
            .Where(x => shopIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var categoryNames = await _db.Categories.AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var productNames = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return Ok(rows.Select(x => new CommissionRuleDto(
            x.Id,
            x.Name,
            x.ScopeType,
            x.ScopeShopId,
            x.ScopeShopId.HasValue && shopNames.TryGetValue(x.ScopeShopId.Value, out var shopName) ? shopName : null,
            x.ScopeCategoryId,
            x.ScopeCategoryId.HasValue && categoryNames.TryGetValue(x.ScopeCategoryId.Value, out var categoryName) ? categoryName : null,
            x.ScopeProductId,
            x.ScopeProductId.HasValue && productNames.TryGetValue(x.ScopeProductId.Value, out var productName) ? productName : null,
            x.Currency,
            x.PlatformFeeMode,
            x.PlatformFeeAmount,
            x.PlatformFeePercent,
            x.MinUnitPrice,
            x.MaxUnitPrice,
            x.StartsAtUtc,
            x.EndsAtUtc,
            x.Priority,
            x.IsActive,
            x.Description,
            x.CreatedAtUtc,
            x.UpdatedAtUtc)).ToList());
    }

    [HttpPost("rules")]
    public async Task<ActionResult<CommissionRuleDto>> CreateRule([FromBody] UpsertCommissionRuleRequest req, CancellationToken ct = default)
    {
        if (!TryValidateRequest(req, out var validationError, out var normalized))
        {
            return BadRequest(validationError);
        }

        if (!await ValidateScopesExistAsync(normalized, ct))
        {
            return BadRequest("Scope introuvable (shop/category/product).");
        }

        var now = DateTime.UtcNow;
        var entity = new Domain.Entities.CommissionRule
        {
            Name = normalized.Name,
            ScopeType = normalized.ScopeType,
            ScopeShopId = normalized.ScopeShopId,
            ScopeCategoryId = normalized.ScopeCategoryId,
            ScopeProductId = normalized.ScopeProductId,
            Currency = normalized.Currency,
            PlatformFeeMode = normalized.PlatformFeeMode,
            PlatformFeeAmount = normalized.PlatformFeeAmount,
            PlatformFeePercent = normalized.PlatformFeePercent,
            MinUnitPrice = normalized.MinUnitPrice,
            MaxUnitPrice = normalized.MaxUnitPrice,
            StartsAtUtc = normalized.StartsAtUtc,
            EndsAtUtc = normalized.EndsAtUtc,
            Priority = normalized.Priority,
            IsActive = normalized.IsActive,
            Description = normalized.Description,
            CreatedAtUtc = now,
            UpdatedAtUtc = null,
            CreatedByUserId = _current.UserId,
            UpdatedByUserId = null
        };

        _db.CommissionRules.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(await ToDtoAsync(entity, ct));
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<ActionResult<CommissionRuleDto>> UpdateRule(Guid id, [FromBody] UpsertCommissionRuleRequest req, CancellationToken ct = default)
    {
        var entity = await _db.CommissionRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound("Regle introuvable.");
        }

        if (!TryValidateRequest(req, out var validationError, out var normalized))
        {
            return BadRequest(validationError);
        }

        if (!await ValidateScopesExistAsync(normalized, ct))
        {
            return BadRequest("Scope introuvable (shop/category/product).");
        }

        entity.Name = normalized.Name;
        entity.ScopeType = normalized.ScopeType;
        entity.ScopeShopId = normalized.ScopeShopId;
        entity.ScopeCategoryId = normalized.ScopeCategoryId;
        entity.ScopeProductId = normalized.ScopeProductId;
        entity.Currency = normalized.Currency;
        entity.PlatformFeeMode = normalized.PlatformFeeMode;
        entity.PlatformFeeAmount = normalized.PlatformFeeAmount;
        entity.PlatformFeePercent = normalized.PlatformFeePercent;
        entity.MinUnitPrice = normalized.MinUnitPrice;
        entity.MaxUnitPrice = normalized.MaxUnitPrice;
        entity.StartsAtUtc = normalized.StartsAtUtc;
        entity.EndsAtUtc = normalized.EndsAtUtc;
        entity.Priority = normalized.Priority;
        entity.IsActive = normalized.IsActive;
        entity.Description = normalized.Description;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedByUserId = _current.UserId;

        await _db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(entity, ct));
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.CommissionRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return NotFound("Regle introuvable.");
        }

        _db.CommissionRules.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("rules/resolve-preview")]
    public async Task<ActionResult<CommissionResolvePreviewDto>> ResolvePreview(
        [FromQuery] Guid productId,
        [FromQuery] decimal unitPrice,
        CancellationToken ct = default)
    {
        if (productId == Guid.Empty)
        {
            return BadRequest("productId requis.");
        }

        if (unitPrice <= 0m)
        {
            return BadRequest("unitPrice doit etre superieur a 0.");
        }

        var product = await _db.Products.AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ShopId,
                x.CategoryId,
                x.Currency,
                x.PlatformFeeMode,
                x.PlatformFeeAmount,
                x.PlatformFeePercent
            })
            .FirstOrDefaultAsync(ct);
        if (product is null)
        {
            return NotFound("Produit introuvable.");
        }

        var now = DateTime.UtcNow;
        var normalizedCurrency = (product.Currency ?? string.Empty).Trim().ToUpperInvariant();

        var candidates = await _db.CommissionRules.AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => !x.StartsAtUtc.HasValue || x.StartsAtUtc.Value <= now)
            .Where(x => !x.EndsAtUtc.HasValue || x.EndsAtUtc.Value > now)
            .Where(x =>
                x.ScopeType == ScopeGlobal ||
                (x.ScopeType == ScopeShop && x.ScopeShopId == product.ShopId) ||
                (x.ScopeType == ScopeCategory && x.ScopeCategoryId == product.CategoryId) ||
                (x.ScopeType == ScopeProduct && x.ScopeProductId == product.Id))
            .Where(x => x.Currency == null || x.Currency == "" || x.Currency == normalizedCurrency)
            .Select(x => new CommissionRulePreviewRow(
                x.Id,
                x.Name,
                x.ScopeType,
                x.PlatformFeeMode,
                x.PlatformFeeAmount,
                x.PlatformFeePercent,
                x.Priority,
                x.CreatedAtUtc,
                x.MinUnitPrice,
                x.MaxUnitPrice))
            .ToListAsync(ct);

        var winner = candidates
            .Where(x => !x.MinUnitPrice.HasValue || unitPrice >= x.MinUnitPrice.Value)
            .Where(x => !x.MaxUnitPrice.HasValue || unitPrice <= x.MaxUnitPrice.Value)
            .OrderByDescending(x => ScopeScore(x.ScopeType))
            .ThenBy(x => x.Priority)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        if (winner is not null)
        {
            var fee = ResolveFeeFromMode(winner.PlatformFeeMode, winner.PlatformFeeAmount, winner.PlatformFeePercent, unitPrice);
            if (fee.HasValue)
            {
                return Ok(new CommissionResolvePreviewDto(
                    product.Id,
                    product.Name,
                    unitPrice,
                    fee.Value,
                    "DynamicRule",
                    winner.Id,
                    winner.Name,
                    winner.ScopeType));
            }
        }

        var fallbackFee = ResolveFeeFromMode(product.PlatformFeeMode, product.PlatformFeeAmount, product.PlatformFeePercent, unitPrice)
            ?? 0m;
        return Ok(new CommissionResolvePreviewDto(
            product.Id,
            product.Name,
            unitPrice,
            fallbackFee,
            "ProductDefault",
            null,
            null,
            null));
    }

    private async Task<CommissionRuleDto> ToDtoAsync(Domain.Entities.CommissionRule entity, CancellationToken ct)
    {
        string? shopName = null;
        string? categoryName = null;
        string? productName = null;

        if (entity.ScopeShopId.HasValue)
        {
            shopName = await _db.Shops.AsNoTracking()
                .Where(x => x.Id == entity.ScopeShopId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
        }

        if (entity.ScopeCategoryId.HasValue)
        {
            categoryName = await _db.Categories.AsNoTracking()
                .Where(x => x.Id == entity.ScopeCategoryId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
        }

        if (entity.ScopeProductId.HasValue)
        {
            productName = await _db.Products.AsNoTracking()
                .Where(x => x.Id == entity.ScopeProductId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new CommissionRuleDto(
            entity.Id,
            entity.Name,
            entity.ScopeType,
            entity.ScopeShopId,
            shopName,
            entity.ScopeCategoryId,
            categoryName,
            entity.ScopeProductId,
            productName,
            entity.Currency,
            entity.PlatformFeeMode,
            entity.PlatformFeeAmount,
            entity.PlatformFeePercent,
            entity.MinUnitPrice,
            entity.MaxUnitPrice,
            entity.StartsAtUtc,
            entity.EndsAtUtc,
            entity.Priority,
            entity.IsActive,
            entity.Description,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private async Task<bool> ValidateScopesExistAsync(NormalizedRuleInput normalized, CancellationToken ct)
    {
        if (normalized.ScopeShopId.HasValue)
        {
            var exists = await _db.Shops.AsNoTracking().AnyAsync(x => x.Id == normalized.ScopeShopId.Value, ct);
            if (!exists)
            {
                return false;
            }
        }

        if (normalized.ScopeCategoryId.HasValue)
        {
            var exists = await _db.Categories.AsNoTracking().AnyAsync(x => x.Id == normalized.ScopeCategoryId.Value, ct);
            if (!exists)
            {
                return false;
            }
        }

        if (normalized.ScopeProductId.HasValue)
        {
            var exists = await _db.Products.AsNoTracking().AnyAsync(x => x.Id == normalized.ScopeProductId.Value, ct);
            if (!exists)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateRequest(
        UpsertCommissionRuleRequest req,
        out string error,
        out NormalizedRuleInput normalized)
    {
        error = string.Empty;
        normalized = new NormalizedRuleInput(
            Name: string.Empty,
            ScopeType: ScopeGlobal,
            ScopeShopId: null,
            ScopeCategoryId: null,
            ScopeProductId: null,
            Currency: null,
            PlatformFeeMode: "Flat",
            PlatformFeeAmount: 0m,
            PlatformFeePercent: null,
            MinUnitPrice: null,
            MaxUnitPrice: null,
            StartsAtUtc: null,
            EndsAtUtc: null,
            Priority: 100,
            IsActive: true,
            Description: null);

        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name requis.";
            return false;
        }

        if (name.Length > 120)
        {
            name = name[..120];
        }

        if (!TryNormalizeScopeType(req.ScopeType, out var scopeType))
        {
            error = "ScopeType invalide.";
            return false;
        }

        var shopId = req.ScopeShopId == Guid.Empty ? null : req.ScopeShopId;
        var categoryId = req.ScopeCategoryId == Guid.Empty ? null : req.ScopeCategoryId;
        var productId = req.ScopeProductId == Guid.Empty ? null : req.ScopeProductId;

        var scopeCount = (shopId.HasValue ? 1 : 0) + (categoryId.HasValue ? 1 : 0) + (productId.HasValue ? 1 : 0);
        switch (scopeType)
        {
            case ScopeGlobal:
                if (scopeCount != 0)
                {
                    error = "Global scope ne doit pas contenir de scope IDs.";
                    return false;
                }

                break;
            case ScopeShop:
                if (!shopId.HasValue || scopeCount != 1)
                {
                    error = "Scope Shop invalide.";
                    return false;
                }

                break;
            case ScopeCategory:
                if (!categoryId.HasValue || scopeCount != 1)
                {
                    error = "Scope Category invalide.";
                    return false;
                }

                break;
            case ScopeProduct:
                if (!productId.HasValue || scopeCount != 1)
                {
                    error = "Scope Product invalide.";
                    return false;
                }

                break;
            default:
                error = "ScopeType invalide.";
                return false;
        }

        if (!TryNormalizeFeeMode(req.PlatformFeeMode, out var feeMode))
        {
            error = "PlatformFeeMode invalide.";
            return false;
        }

        decimal feeAmount;
        decimal? feePercent;
        if (feeMode == "Flat")
        {
            feeAmount = req.PlatformFeeAmount ?? 0m;
            if (feeAmount < 0m)
            {
                error = "PlatformFeeAmount invalide.";
                return false;
            }

            feePercent = null;
        }
        else
        {
            if (!req.PlatformFeePercent.HasValue || req.PlatformFeePercent.Value <= 0m || req.PlatformFeePercent.Value > 100m)
            {
                error = "PlatformFeePercent doit etre > 0 et <= 100.";
                return false;
            }

            feePercent = req.PlatformFeePercent.Value;
            feeAmount = 0m;
        }

        if (req.MinUnitPrice.HasValue && req.MinUnitPrice.Value < 0m)
        {
            error = "MinUnitPrice invalide.";
            return false;
        }

        if (req.MaxUnitPrice.HasValue && req.MaxUnitPrice.Value < 0m)
        {
            error = "MaxUnitPrice invalide.";
            return false;
        }

        if (req.MinUnitPrice.HasValue && req.MaxUnitPrice.HasValue && req.MinUnitPrice.Value > req.MaxUnitPrice.Value)
        {
            error = "MinUnitPrice ne peut pas depasser MaxUnitPrice.";
            return false;
        }

        var startsAtUtc = req.StartsAtUtc?.ToUniversalTime();
        var endsAtUtc = req.EndsAtUtc?.ToUniversalTime();
        if (startsAtUtc.HasValue && endsAtUtc.HasValue && endsAtUtc.Value <= startsAtUtc.Value)
        {
            error = "Fenetre temporelle invalide.";
            return false;
        }

        var currency = NormalizeCurrency(req.Currency);
        if (req.Currency is not null && currency is null)
        {
            error = "Currency invalide.";
            return false;
        }

        var priority = Math.Clamp(req.Priority ?? 100, -1000, 1000);
        var description = NormalizeDescription(req.Description);

        normalized = new NormalizedRuleInput(
            name,
            scopeType,
            shopId,
            categoryId,
            productId,
            currency,
            feeMode,
            feeAmount,
            feePercent,
            req.MinUnitPrice,
            req.MaxUnitPrice,
            startsAtUtc,
            endsAtUtc,
            priority,
            req.IsActive ?? true,
            description);
        return true;
    }

    private static string? NormalizeDescription(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private static string? NormalizeCurrency(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length == 3 ? trimmed : null;
    }

    private static bool TryNormalizeScopeType(string? raw, out string normalized)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "global":
            case "":
                normalized = ScopeGlobal;
                return true;
            case "shop":
                normalized = ScopeShop;
                return true;
            case "category":
                normalized = ScopeCategory;
                return true;
            case "product":
                normalized = ScopeProduct;
                return true;
            default:
                normalized = string.Empty;
                return false;
        }
    }

    private static bool TryNormalizeFeeMode(string? raw, out string normalized)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "flat":
            case "":
                normalized = "Flat";
                return true;
            case "percent":
                normalized = "Percent";
                return true;
            default:
                normalized = string.Empty;
                return false;
        }
    }

    private static int ScopeScore(string scopeType)
        => scopeType switch
        {
            ScopeProduct => 4,
            ScopeCategory => 3,
            ScopeShop => 2,
            ScopeGlobal => 1,
            _ => 0
        };

    private static decimal? ResolveFeeFromMode(string mode, decimal feeAmount, decimal? feePercent, decimal unitPrice)
    {
        if (unitPrice <= 0m)
        {
            return 0m;
        }

        if (string.Equals(mode, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var percent = feePercent ?? 0m;
            if (percent <= 0m)
            {
                return 0m;
            }

            var computed = decimal.Round(unitPrice * (percent / 100m), 2, MidpointRounding.AwayFromZero);
            return decimal.Min(unitPrice, decimal.Max(0m, computed));
        }

        if (!string.Equals(mode, "Flat", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return decimal.Min(unitPrice, decimal.Max(0m, feeAmount));
    }

    private const string ScopeGlobal = "Global";
    private const string ScopeShop = "Shop";
    private const string ScopeCategory = "Category";
    private const string ScopeProduct = "Product";

    public sealed record UpsertCommissionRuleRequest(
        string? Name,
        string? ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        Guid? ScopeProductId,
        string? Currency,
        string? PlatformFeeMode,
        decimal? PlatformFeeAmount,
        decimal? PlatformFeePercent,
        decimal? MinUnitPrice,
        decimal? MaxUnitPrice,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        int? Priority,
        bool? IsActive,
        string? Description);

    public sealed record CommissionRuleDto(
        Guid Id,
        string Name,
        string ScopeType,
        Guid? ScopeShopId,
        string? ScopeShopName,
        Guid? ScopeCategoryId,
        string? ScopeCategoryName,
        Guid? ScopeProductId,
        string? ScopeProductName,
        string? Currency,
        string PlatformFeeMode,
        decimal PlatformFeeAmount,
        decimal? PlatformFeePercent,
        decimal? MinUnitPrice,
        decimal? MaxUnitPrice,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        int Priority,
        bool IsActive,
        string? Description,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    public sealed record CommissionResolvePreviewDto(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        decimal AppliedFee,
        string Source,
        Guid? RuleId,
        string? RuleName,
        string? RuleScopeType);

    private sealed record NormalizedRuleInput(
        string Name,
        string ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        Guid? ScopeProductId,
        string? Currency,
        string PlatformFeeMode,
        decimal PlatformFeeAmount,
        decimal? PlatformFeePercent,
        decimal? MinUnitPrice,
        decimal? MaxUnitPrice,
        DateTime? StartsAtUtc,
        DateTime? EndsAtUtc,
        int Priority,
        bool IsActive,
        string? Description);

    private sealed record CommissionRulePreviewRow(
        Guid Id,
        string Name,
        string ScopeType,
        string PlatformFeeMode,
        decimal PlatformFeeAmount,
        decimal? PlatformFeePercent,
        int Priority,
        DateTime CreatedAtUtc,
        decimal? MinUnitPrice,
        decimal? MaxUnitPrice);
}
