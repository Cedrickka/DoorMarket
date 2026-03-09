using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminProductsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminProductsController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("fees")]
    public async Task<ActionResult<AdminProductFeesPaged>> GetFees(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Products.AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.Shop.Name.Contains(s) || p.Category.Name.Contains(s));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProductFeeRow(
                p.Id,
                p.Name,
                p.ShopId,
                p.Shop.Name,
                p.CategoryId,
                p.Category.Name,
                p.Price,
                p.PlatformFeeMode,
                p.PlatformFeeAmount,
                p.PlatformFeePercent,
                p.Currency,
                p.IsActive))
            .ToListAsync(ct);

        items = items
            .Select(x =>
            {
                var mode = NormalizeFeeMode(x.PlatformFeeMode) ?? "Flat";
                return x with
                {
                    PlatformFeeMode = mode,
                    PlatformFeeAmount = mode == "Flat" ? decimal.Max(0m, x.PlatformFeeAmount) : 0m,
                    PlatformFeePercent = mode == "Percent" ? (x.PlatformFeePercent ?? 1m) : null
                };
            })
            .ToList();

        return Ok(new AdminProductFeesPaged(items, total, page, pageSize));
    }

    [HttpPut("{id:guid}/fee")]
    public async Task<IActionResult> UpdateFee(Guid id, [FromBody] UpdateProductFeeRequest req, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return NotFound();

        if (!TryApplyFee(product, req.PlatformFeeMode, req.PlatformFeeAmount, req.PlatformFeePercent, out var error))
        {
            return BadRequest(error);
        }

        product.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("fees/bulk-by-ids")]
    public async Task<ActionResult<BulkUpdateProductFeesResult>> BulkUpdateFeesByIds(
        [FromBody] BulkUpdateProductFeesRequest req,
        CancellationToken ct = default)
    {
        var ids = (req.ProductIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Take(500)
            .ToList();

        if (ids.Count == 0)
        {
            return BadRequest("Aucun produit valide fourni.");
        }

        var products = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            return NotFound("Produits introuvables.");
        }

        var updated = 0;
        var skipped = 0;
        foreach (var product in products)
        {
            if (req.OnlyActive.GetValueOrDefault(true) && !product.IsActive)
            {
                skipped++;
                continue;
            }

            if (!TryApplyFee(product, req.PlatformFeeMode, req.PlatformFeeAmount, req.PlatformFeePercent, out var error))
            {
                return BadRequest($"Produit '{product.Name}': {error}");
            }

            product.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
        }

        if (updated == 0)
        {
            return Ok(new BulkUpdateProductFeesResult(0, skipped, products.Count));
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new BulkUpdateProductFeesResult(updated, skipped, products.Count));
    }

    private static bool TryApplyFee(Domain.Entities.Product product, string? modeRaw, decimal? feeAmount, decimal? feePercent, out string error)
    {
        var mode = NormalizeFeeMode(modeRaw);
        if (mode is null)
        {
            error = "Mode de commission invalide.";
            return false;
        }

        if (mode == "Flat")
        {
            var amount = feeAmount ?? 0m;
            if (amount < 0m)
            {
                error = "Commission invalide.";
                return false;
            }

            if (amount > product.Price)
            {
                error = "Commission invalide: superieure au prix produit.";
                return false;
            }

            product.PlatformFeeMode = "Flat";
            product.PlatformFeeAmount = amount;
            product.PlatformFeePercent = null;
            error = string.Empty;
            return true;
        }

        if (!feePercent.HasValue)
        {
            error = "PlatformFeePercent requis pour le mode Percent.";
            return false;
        }

        var percent = feePercent.Value;
        if (percent <= 0m || percent > 100m)
        {
            error = "PlatformFeePercent doit etre entre 0 et 100.";
            return false;
        }

        product.PlatformFeeMode = "Percent";
        product.PlatformFeePercent = percent;
        product.PlatformFeeAmount = 0m;
        error = string.Empty;
        return true;
    }

    private static string? NormalizeFeeMode(string? rawMode)
    {
        var mode = (rawMode ?? string.Empty).Trim().ToLowerInvariant();
        return mode switch
        {
            "" => "Flat",
            "flat" => "Flat",
            "percent" => "Percent",
            _ => null
        };
    }

    public sealed record UpdateProductFeeRequest(string? PlatformFeeMode, decimal? PlatformFeeAmount, decimal? PlatformFeePercent);
    public sealed record BulkUpdateProductFeesRequest(List<Guid>? ProductIds, string? PlatformFeeMode, decimal? PlatformFeeAmount, decimal? PlatformFeePercent, bool? OnlyActive);
    public sealed record BulkUpdateProductFeesResult(int UpdatedCount, int SkippedCount, int RequestedCount);

    public sealed record AdminProductFeeRow(
        Guid Id,
        string Name,
        Guid ShopId,
        string ShopName,
        Guid CategoryId,
        string CategoryName,
        decimal Price,
        string PlatformFeeMode,
        decimal PlatformFeeAmount,
        decimal? PlatformFeePercent,
        string Currency,
        bool IsActive);

    public sealed record AdminProductFeesPaged(List<AdminProductFeeRow> Items, int Total, int Page, int PageSize);
}
