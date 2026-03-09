using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Products;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.Products;

public class ProductService : IProductService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public ProductService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    // PUBLIC
    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        var q = _db.Products.AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Category)
            .AsQueryable();

        if (query.ActiveOnly)
            q = q.Where(p => p.IsActive);

        if (query.ShopId is not null)
            q = q.Where(p => p.ShopId == query.ShopId);

        if (query.CategoryId is not null)
            q = q.Where(p => p.CategoryId == query.CategoryId);

        if (!string.IsNullOrWhiteSpace(query.CountryTag))
        {
            var c = query.CountryTag.Trim().ToUpperInvariant();
            q = q.Where(p => p.Shop.CountryTag == c);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(p => p.Shop.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var s = query.Q.Trim();
            q = q.Where(p => p.Name.Contains(s) || (p.Description != null && p.Description.Contains(s)));
        }

        if (query.MinPrice is not null)
            q = q.Where(p => p.Price >= query.MinPrice.Value);

        if (query.MaxPrice is not null)
            q = q.Where(p => p.Price <= query.MaxPrice.Value);

        if (query.InStockOnly)
            q = q.Where(p => p.StockQty > 0);

        if (query.PromotedOnly)
        {
            var now = DateTime.UtcNow;
            q = q.Where(p =>
                p.IsPromotionEnabled &&
                p.PromotionPrice.HasValue &&
                p.PromotionPrice > 0m &&
                p.PromotionPrice < p.Price &&
                (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= now) &&
                (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= now));
        }

        q = q.OrderByDescending(p => p.CreatedAtUtc);

        return await ToPagedAsync(q, query.Page, query.PageSize, ct);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Products.AsNoTracking()
            .Include(x => x.Shop)
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p is null)
        {
            return null;
        }

        var reviewAggregate = await _db.ShopReviews.AsNoTracking()
            .Where(x => x.ShopId == p.ShopId)
            .GroupBy(x => x.ShopId)
            .Select(g => new ShopReviewAggregate(
                g.Key,
                g.Average(r => (double)r.Rating),
                g.Count()))
            .FirstOrDefaultAsync(ct);

        return ToDto(
            p,
            reviewAggregate?.Rating,
            reviewAggregate?.Count ?? 0);
    }

    // OWNER
    public async Task<ProductDto> CreateMyProductAsync(ProductCreateRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        if (!shop.IsVerified)
            throw new InvalidOperationException("Votre boutique n'est pas encore validée par l'admin.");

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct)
                       ?? throw new InvalidOperationException("Catégorie invalide.");

        var (promotionStartUtc, promotionEndUtc) = NormalizePromotionWindow(req.PromotionStartUtc, req.PromotionEndUtc);

        var resolvedPromotionPrice = ResolvePromotionPrice(
            req.Price,
            req.IsPromotionEnabled,
            req.PromotionPrice,
            req.PromotionPercent,
            promotionStartUtc,
            promotionEndUtc);

        var p = new Product
        {
            ShopId = shop.Id,
            CategoryId = category.Id,
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Price = req.Price,
            IsPromotionEnabled = req.IsPromotionEnabled,
            PromotionPrice = req.IsPromotionEnabled ? resolvedPromotionPrice : null,
            PromotionStartUtc = req.IsPromotionEnabled ? promotionStartUtc : null,
            PromotionEndUtc = req.IsPromotionEnabled ? promotionEndUtc : null,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            StockQty = req.StockQty,
            IsActive = true,
            MainImageUrl = string.IsNullOrWhiteSpace(req.MainImageUrl) ? null : req.MainImageUrl.Trim()
        };

        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);

        // reload for dto with includes
        var created = await _db.Products.AsNoTracking()
            .Include(x => x.Shop)
            .Include(x => x.Category)
            .FirstAsync(x => x.Id == p.Id, ct);

        return ToDto(created, null, 0);
    }

    public async Task<ProductDto> UpdateMyProductAsync(Guid productId, ProductUpdateRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.ShopId == shop.Id, ct)
                ?? throw new InvalidOperationException("Produit introuvable.");

        // check category exists
        var catExists = await _db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct);
        if (!catExists) throw new InvalidOperationException("Catégorie invalide.");

        var (promotionStartUtc, promotionEndUtc) = NormalizePromotionWindow(req.PromotionStartUtc, req.PromotionEndUtc);

        var resolvedPromotionPrice = ResolvePromotionPrice(
            req.Price,
            req.IsPromotionEnabled,
            req.PromotionPrice,
            req.PromotionPercent,
            promotionStartUtc,
            promotionEndUtc);

        p.CategoryId = req.CategoryId;
        p.Name = req.Name.Trim();
        p.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        p.Price = req.Price;
        p.IsPromotionEnabled = req.IsPromotionEnabled;
        p.PromotionPrice = req.IsPromotionEnabled ? resolvedPromotionPrice : null;
        p.PromotionStartUtc = req.IsPromotionEnabled ? promotionStartUtc : null;
        p.PromotionEndUtc = req.IsPromotionEnabled ? promotionEndUtc : null;
        p.Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant();
        p.StockQty = req.StockQty;
        p.IsActive = req.IsActive;
        p.MainImageUrl = string.IsNullOrWhiteSpace(req.MainImageUrl) ? null : req.MainImageUrl.Trim();
        p.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Products.AsNoTracking()
            .Include(x => x.Shop)
            .Include(x => x.Category)
            .FirstAsync(x => x.Id == p.Id, ct);

        return ToDto(updated, null, 0);
    }

    public async Task<PagedResult<ProductDto>> GetMyProductsAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var q = _db.Products.AsNoTracking()
            .Include(p => p.Shop)
            .Include(p => p.Category)
            .Where(p => p.ShopId == shop.Id)
            .OrderByDescending(p => p.CreatedAtUtc);

        return await ToPagedAsync(q, page, pageSize, ct);
    }

    public async Task SetActiveAsync(Guid productId, bool isActive, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var role = _current.Role;

        var p = await _db.Products.Include(x => x.Shop).FirstOrDefaultAsync(x => x.Id == productId, ct)
                ?? throw new InvalidOperationException("Produit introuvable.");

        // Admin peut tout, sinon owner seulement
        if (!string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                       ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

            if (p.ShopId != shop.Id)
                throw new InvalidOperationException("Accès refusé.");
        }

        p.IsActive = isActive;
        p.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task AdjustStockAsync(Guid productId, int newStockQty, CancellationToken ct)
    {
        if (newStockQty < 0) throw new InvalidOperationException("Stock invalide.");

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.ShopId == shop.Id, ct)
                ?? throw new InvalidOperationException("Produit introuvable.");

        p.StockQty = newStockQty;
        p.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<PagedResult<ProductDto>> ToPagedAsync(IQueryable<Product> q, int page, int pageSize, CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await q.CountAsync(ct);

        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var shopIds = list.Select(x => x.ShopId).Distinct().ToList();
        var reviewLookup = shopIds.Count == 0
            ? new Dictionary<Guid, ShopReviewAggregate>()
            : await _db.ShopReviews.AsNoTracking()
                .Where(x => shopIds.Contains(x.ShopId))
                .GroupBy(x => x.ShopId)
                .Select(g => new ShopReviewAggregate(
                    g.Key,
                    g.Average(r => (double)r.Rating),
                    g.Count()))
                .ToDictionaryAsync(x => x.ShopId, ct);

        var items = list.Select(product =>
        {
            var hasReview = reviewLookup.TryGetValue(product.ShopId, out var review);
            return ToDto(
                product,
                hasReview ? review!.Rating : null,
                hasReview ? review!.Count : 0);
        }).ToList();

        return new PagedResult<ProductDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    private static ProductDto ToDto(Product p, double? shopRating, int shopReviewCount)
    {
        var now = DateTime.UtcNow;
        var hasActivePromotion = p.HasActivePromotion(now);
        var effectivePrice = p.GetEffectivePrice(now);
        var promotionPercent = p.GetPromotionPercent();

        return new ProductDto(
            p.Id,
            p.ShopId,
            p.Shop.Name,
            p.CategoryId,
            p.Category.Name,
            p.Category.NameEn,
            p.Name,
            p.Description,
            p.Price,
            effectivePrice,
            hasActivePromotion,
            p.IsPromotionEnabled,
            p.PromotionPrice,
            promotionPercent,
            p.PromotionStartUtc,
            p.PromotionEndUtc,
            p.Currency,
            p.StockQty,
            p.IsActive,
            p.MainImageUrl,
            p.CreatedAtUtc,
            shopRating,
            shopReviewCount
        );
    }

    private sealed record ShopReviewAggregate(
        Guid ShopId,
        double Rating,
        int Count);

    private static decimal? ResolvePromotionPrice(
        decimal basePrice,
        bool isPromotionEnabled,
        decimal? promotionPrice,
        decimal? promotionPercent,
        DateTime? promotionStartUtc,
        DateTime? promotionEndUtc)
    {
        if (basePrice <= 0m)
        {
            throw new InvalidOperationException("Le prix doit etre superieur a 0.");
        }

        if (!isPromotionEnabled)
        {
            return null;
        }

        if (promotionStartUtc.HasValue && promotionEndUtc.HasValue && promotionStartUtc > promotionEndUtc)
        {
            throw new InvalidOperationException("La date de debut promotion doit etre avant la date de fin.");
        }

        if (promotionPercent.HasValue && (promotionPercent.Value <= 0m || promotionPercent.Value >= 100m))
        {
            throw new InvalidOperationException("Le pourcentage de promotion doit etre entre 0 et 100.");
        }

        if (!promotionPrice.HasValue && !promotionPercent.HasValue)
        {
            throw new InvalidOperationException("Renseignez un prix promo ou un pourcentage de reduction.");
        }

        if (promotionPrice.HasValue && (promotionPrice.Value <= 0m || promotionPrice.Value >= basePrice))
        {
            throw new InvalidOperationException("Le prix promotionnel doit etre inferieur au prix normal.");
        }

        var resolvedPromotionPrice = promotionPrice;
        if (!resolvedPromotionPrice.HasValue && promotionPercent.HasValue)
        {
            resolvedPromotionPrice = decimal.Round(
                basePrice * (1m - (promotionPercent.Value / 100m)),
                2,
                MidpointRounding.AwayFromZero);
        }

        if (!resolvedPromotionPrice.HasValue || resolvedPromotionPrice.Value <= 0m || resolvedPromotionPrice.Value >= basePrice)
        {
            throw new InvalidOperationException("Prix promotionnel invalide.");
        }

        if (promotionPrice.HasValue && promotionPercent.HasValue)
        {
            var expectedFromPercent = decimal.Round(
                basePrice * (1m - (promotionPercent.Value / 100m)),
                2,
                MidpointRounding.AwayFromZero);

            if (Math.Abs(expectedFromPercent - promotionPrice.Value) > 0.01m)
            {
                throw new InvalidOperationException("Le prix promo et le pourcentage ne correspondent pas.");
            }
        }

        return resolvedPromotionPrice;
    }

    private static (DateTime? StartUtc, DateTime? EndUtc) NormalizePromotionWindow(DateTime? promotionStartUtc, DateTime? promotionEndUtc)
    {
        var normalizedStart = NormalizePromotionDate(promotionStartUtc, endOfDay: false);
        var normalizedEnd = NormalizePromotionDate(promotionEndUtc, endOfDay: true);

        if (normalizedStart.HasValue && normalizedEnd.HasValue && normalizedStart > normalizedEnd)
        {
            throw new InvalidOperationException("La date de debut promotion doit etre avant la date de fin.");
        }

        return (normalizedStart, normalizedEnd);
    }

    private static DateTime? NormalizePromotionDate(DateTime? value, bool endOfDay)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var dt = value.Value;
        if (endOfDay && dt.TimeOfDay == TimeSpan.Zero)
        {
            dt = dt.Date.AddDays(1).AddTicks(-1);
        }

        if (dt.Kind == DateTimeKind.Utc)
        {
            return dt;
        }

        if (dt.Kind == DateTimeKind.Unspecified)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
        }

        return dt.ToUniversalTime();
    }
}
