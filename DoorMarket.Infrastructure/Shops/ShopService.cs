using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Shops;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.Shops;

public class ShopService : IShopService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public ShopService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<ShopDto> CreateMyShopAsync(ShopCreateRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var user = await _db.Users.Include(x => x.Shop)
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (user.Shop is not null)
            throw new InvalidOperationException("Vous avez déjà une boutique.");

        var shop = new Shop
        {
            OwnerUserId = user.Id,
            Name = req.Name.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            CountryTag = req.CountryTag.Trim().ToUpperInvariant(),
            City = req.City.Trim(),
            IsVerified = false
        };

        // V1 : dès qu’un user crée une boutique, on le met rôle Shop (simple et efficace)
        user.Role = UserRole.Shop;

        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);

        return ToDto(shop, null, 0);
    }

    public async Task<ShopDto?> GetMyShopAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);

        if (shop is null) return null;
        var rating = await GetRatingAsync(shop.Id, ct);
        return ToDto(shop, rating.Rating, rating.ReviewCount);
    }

    public async Task<ShopDto> UpdateMyShopAsync(ShopUpdateRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Aucune boutique trouvée pour cet utilisateur.");

        shop.Name = req.Name.Trim();
        if (!string.IsNullOrWhiteSpace(req.ImageUrl))
        {
            shop.ImageUrl = req.ImageUrl.Trim();
        }
        shop.CountryTag = req.CountryTag.Trim().ToUpperInvariant();
        shop.City = req.City.Trim();
        shop.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var rating = await GetRatingAsync(shop.Id, ct);
        return ToDto(shop, rating.Rating, rating.ReviewCount);
    }

    public async Task<ShopDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (shop is null) return null;
        var rating = await GetRatingAsync(shop.Id, ct);
        return ToDto(shop, rating.Rating, rating.ReviewCount);
    }

    public async Task<PagedResult<ShopDto>> SearchAsync(ShopQuery query, CancellationToken ct)
    {
        var q = _db.Shops.AsNoTracking().AsQueryable();

        if (query.VerifiedOnly)
            q = q.Where(x => x.IsVerified);

        q = ApplyFilters(q, query);
        q = ApplySort(q, query);

        return await ToPagedAsync(q, query.Page, query.PageSize, ct);
    }

    public async Task<PagedResult<ShopDto>> AdminSearchAsync(ShopQuery query, CancellationToken ct)
    {
        // Admin voit tout (verifiedOnly optionnel)
        var q = _db.Shops.AsNoTracking().AsQueryable();

        if (query.VerifiedOnly)
            q = q.Where(x => x.IsVerified);

        q = ApplyFilters(q, query);
        q = ApplySort(q, query);

        return await ToPagedAsync(q, query.Page, query.PageSize, ct);
    }

    public async Task VerifyAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(x => x.Id == shopId, ct)
                   ?? throw new InvalidOperationException("Boutique introuvable.");

        shop.IsVerified = true;
        shop.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ShopDto> AdminUpdateAsync(Guid shopId, ShopUpdateRequest req, CancellationToken ct)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(x => x.Id == shopId, ct)
                   ?? throw new InvalidOperationException("Boutique introuvable.");

        shop.Name = req.Name.Trim();
        if (!string.IsNullOrWhiteSpace(req.ImageUrl))
        {
            shop.ImageUrl = req.ImageUrl.Trim();
        }
        shop.CountryTag = req.CountryTag.Trim().ToUpperInvariant();
        shop.City = req.City.Trim();
        shop.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var rating = await GetRatingAsync(shop.Id, ct);
        return ToDto(shop, rating.Rating, rating.ReviewCount);
    }

    public async Task AdminDeleteAsync(Guid shopId, CancellationToken ct)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(x => x.Id == shopId, ct)
                   ?? throw new InvalidOperationException("Boutique introuvable.");

        var hasProducts = await _db.Products.AnyAsync(x => x.ShopId == shopId, ct);
        if (hasProducts)
            throw new InvalidOperationException("Impossible de supprimer: la boutique contient des produits.");

        _db.Shops.Remove(shop);
        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<Shop> ApplyFilters(IQueryable<Shop> q, ShopQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.CountryTag))
        {
            var c = query.CountryTag.Trim().ToUpperInvariant();
            q = q.Where(x => x.CountryTag == c);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(x => x.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var search = query.Q.Trim();
            q = q.Where(x => x.Name.Contains(search) || x.City.Contains(search) || x.CountryTag.Contains(search));
        }

        if (query.CategoryId.HasValue)
        {
            var categoryId = query.CategoryId.Value;
            q = q.Where(x => _db.Products.Any(p => p.ShopId == x.Id && p.CategoryId == categoryId && p.IsActive));
        }

        if (query.Recommended == true)
        {
            q = q.Where(x => _db.Products.Any(p => p.ShopId == x.Id && p.IsActive));
        }

        return q;
    }

    private IQueryable<Shop> ApplySort(IQueryable<Shop> q, ShopQuery query)
    {
        if (query.Recommended == true)
        {
            return q
                .OrderByDescending(x => _db.OrderItems.Count(oi => oi.Product.ShopId == x.Id))
                .ThenByDescending(x => x.CreatedAtUtc);
        }

        return q.OrderByDescending(x => x.CreatedAtUtc);
    }

    private async Task<PagedResult<ShopDto>> ToPagedAsync(IQueryable<Shop> q, int page, int pageSize, CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var total = await q.CountAsync(ct);
        var pageItems = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var shopIds = pageItems.Select(s => s.Id).ToList();

        var ratings = await _db.ShopReviews.AsNoTracking()
            .Where(r => shopIds.Contains(r.ShopId))
            .GroupBy(r => r.ShopId)
            .Select(g => new
            {
                ShopId = g.Key,
                Rating = (double?)g.Average(r => (double)r.Rating),
                ReviewCount = g.Count()
            })
            .ToDictionaryAsync(x => x.ShopId, ct);

        var items = pageItems.Select(s =>
        {
            var hasRating = ratings.TryGetValue(s.Id, out var rating);
            return new ShopDto(
                s.Id,
                s.Name,
                s.ImageUrl,
                s.CountryTag,
                s.City,
                s.IsVerified,
                s.CreatedAtUtc,
                hasRating ? rating!.Rating : null,
                hasRating ? rating!.ReviewCount : 0
            );
        }).ToList();

        return new PagedResult<ShopDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    private static ShopDto ToDto(Shop s, double? rating, int reviewCount) =>
        new(s.Id, s.Name, s.ImageUrl, s.CountryTag, s.City, s.IsVerified, s.CreatedAtUtc, rating, reviewCount);

    private async Task<(double? Rating, int ReviewCount)> GetRatingAsync(Guid shopId, CancellationToken ct)
    {
        var summary = await _db.ShopReviews.AsNoTracking()
            .Where(r => r.ShopId == shopId)
            .GroupBy(r => r.ShopId)
            .Select(g => new { Rating = g.Average(r => (double)r.Rating), ReviewCount = g.Count() })
            .FirstOrDefaultAsync(ct);

        return summary is null ? (null, 0) : (summary.Rating, summary.ReviewCount);
    }

    public async Task<ShopDashboardSummaryDto> GetMySummaryAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Commandes du jour (distinct orders contenant un produit de la boutique)
        var orderIdsToday = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Product.ShopId == shop.Id &&
                         oi.Order.CreatedAtUtc >= today && oi.Order.CreatedAtUtc < tomorrow)
            .Select(oi => oi.OrderId)
            .Distinct()
            .ToListAsync(ct);

        var ordersToday = orderIdsToday.Count;

        // Revenus du jour (uniquement payées) => somme des lignes de produits de la boutique
        var revenueToday = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Product.ShopId == shop.Id &&
                         oi.Order.PaymentStatus == "Paid" &&
                         oi.Order.CreatedAtUtc >= today && oi.Order.CreatedAtUtc < tomorrow)
            .SumAsync(oi => (decimal?)oi.LineTotal, ct) ?? 0m;

        var productsActive = await _db.Products.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && p.IsActive)
            .CountAsync(ct);

        var productsOutOfStock = await _db.Products.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && p.IsActive && p.StockQty <= 0)
            .CountAsync(ct);

        return new ShopDashboardSummaryDto(
            ordersToday,
            revenueToday,
            productsActive,
            productsOutOfStock
        );
    }

}
