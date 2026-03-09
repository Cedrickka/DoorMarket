using DoorMarket.Api.Utils;
using DoorMarket.Api.Services;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.DTOs.Search;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/search")]
[EnableRateLimiting("search")]
public class SearchController : ControllerBase
{
    private const int DefaultLimit = 12;
    private const int MaxLimit = 25;
    private const int CandidatePoolPerType = 30;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int PopularQueryCandidatePool = 120;
    private const string TargetTypeProduct = "Product";
    private const string TargetTypeShop = "Shop";
    private const string TargetTypeCategory = "Category";
    private const string TargetTypeQuery = "Query";

    private readonly DoorMarketDbContext _db;

    public SearchController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<SearchSuggestionDto>>> GetSuggestions(
        [FromQuery] string? q,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        var queryResolution = await ResolveQueryRuleAsync(q, ct);
        var normalizedQuery = queryResolution.UserQuery;
        if (normalizedQuery is null || normalizedQuery.Length < 2)
        {
            return Ok(Array.Empty<SearchSuggestionDto>());
        }

        var textTokens = SearchTextNormalizer.Tokenize(queryResolution.EffectiveQuery);
        var scoringQuery = queryResolution.UserQuery ?? queryResolution.EffectiveQuery ?? string.Empty;
        var nowUtc = DateTime.UtcNow;
        var normalizedLimit = NormalizeLimit(limit);

        var productsQuery = _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQty > 0);
        if (textTokens.Count > 0)
        {
            productsQuery = ApplyProductTextFilter(productsQuery, textTokens);
        }
        else if (queryResolution.TargetType == TargetTypeCategory && queryResolution.TargetId.HasValue)
        {
            var categoryId = queryResolution.TargetId.Value;
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
        }
        else if (queryResolution.TargetType == TargetTypeShop && queryResolution.TargetId.HasValue)
        {
            var shopId = queryResolution.TargetId.Value;
            productsQuery = productsQuery.Where(p => p.ShopId == shopId);
        }
        else if (queryResolution.TargetType == TargetTypeProduct && queryResolution.TargetId.HasValue)
        {
            var productId = queryResolution.TargetId.Value;
            productsQuery = productsQuery.Where(p => p.Id == productId);
        }

        var products = await productsQuery
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(CandidatePoolPerType)
            .Select(p => new ProductSuggestionRow(
                p.Id,
                p.Name,
                p.Shop.Name,
                p.IsPromotionEnabled &&
                p.PromotionPrice.HasValue &&
                p.PromotionPrice > 0m &&
                p.PromotionPrice < p.Price &&
                (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)))
            .ToListAsync(ct);

        var shopsQuery = _db.Shops.AsNoTracking().AsQueryable();
        if (textTokens.Count > 0)
        {
            shopsQuery = ApplyShopTextFilter(shopsQuery, textTokens);
        }
        else if (queryResolution.TargetType == TargetTypeShop && queryResolution.TargetId.HasValue)
        {
            var shopId = queryResolution.TargetId.Value;
            shopsQuery = shopsQuery.Where(s => s.Id == shopId);
        }
        else if (queryResolution.TargetType == TargetTypeCategory && queryResolution.TargetId.HasValue)
        {
            var categoryId = queryResolution.TargetId.Value;
            shopsQuery = shopsQuery.Where(s => _db.Products.Any(p => p.ShopId == s.Id && p.CategoryId == categoryId && p.IsActive));
        }

        var shops = await shopsQuery
            .OrderByDescending(s => s.IsVerified)
            .ThenBy(s => s.Name)
            .Take(CandidatePoolPerType)
            .Select(s => new ShopSuggestionRow(
                s.Id,
                s.Name,
                s.City,
                s.CountryTag,
                s.IsVerified))
            .ToListAsync(ct);

        var categoriesQuery = _db.Categories.AsNoTracking().AsQueryable();
        if (textTokens.Count > 0)
        {
            categoriesQuery = ApplyCategoryTextFilter(categoriesQuery, textTokens);
        }
        else if (queryResolution.TargetType == TargetTypeCategory && queryResolution.TargetId.HasValue)
        {
            var categoryId = queryResolution.TargetId.Value;
            categoriesQuery = categoriesQuery.Where(c => c.Id == categoryId);
        }

        var categories = await categoriesQuery
            .OrderBy(c => c.Name)
            .Take(CandidatePoolPerType)
            .Select(c => new CategorySuggestionRow(
                c.Id,
                c.Name,
                c.NameEn))
            .ToListAsync(ct);

        var candidates = new List<SuggestionCandidate>(products.Count + shops.Count + categories.Count + 8);

        var mappedSuggestion = await BuildMappedSuggestionCandidateAsync(queryResolution, ct);
        if (mappedSuggestion is not null)
        {
            candidates.Add(mappedSuggestion);
        }

        var popularQueries = await GetPopularQueriesAsync(
            SearchTextNormalizer.Tokenize(queryResolution.UserQuery),
            ct);
        foreach (var popular in popularQueries)
        {
            var score = ScoreLabel(popular.Query, scoringQuery) + Math.Min(60, popular.Searches * 4) + 20;
            candidates.Add(new SuggestionCandidate(
                score,
                TypeOrder: 3,
                Dto: new SearchSuggestionDto(
                    Type: TargetTypeQuery,
                    EntityId: null,
                    Value: popular.Query,
                    Label: popular.Query,
                    Subtitle: $"popular ({popular.Searches})")));
        }

        foreach (var product in products)
        {
            var score = ScoreLabel(product.Name, scoringQuery);
            if (product.HasActivePromotion)
            {
                score += 15;
            }

            if (score <= 0)
            {
                continue;
            }

            candidates.Add(new SuggestionCandidate(
                score,
                TypeOrder: 0,
                Dto: new SearchSuggestionDto(
                    Type: "Product",
                    EntityId: product.Id,
                    Value: product.Name,
                    Label: product.Name,
                    Subtitle: product.ShopName)));
        }

        foreach (var shop in shops)
        {
            var score = ScoreLabel(shop.Name, scoringQuery);
            if (shop.IsVerified)
            {
                score += 10;
            }

            if (score <= 0)
            {
                continue;
            }

            candidates.Add(new SuggestionCandidate(
                score,
                TypeOrder: 1,
                Dto: new SearchSuggestionDto(
                    Type: "Shop",
                    EntityId: shop.Id,
                    Value: shop.Name,
                    Label: shop.Name,
                    Subtitle: $"{shop.City}, {shop.CountryTag}")));
        }

        foreach (var category in categories)
        {
            var score = ScoreLabel(category.Name, scoringQuery);
            if (score <= 0 && !string.IsNullOrWhiteSpace(category.NameEn))
            {
                score = ScoreLabel(category.NameEn, scoringQuery);
            }

            if (score <= 0)
            {
                continue;
            }

            candidates.Add(new SuggestionCandidate(
                score,
                TypeOrder: 2,
                Dto: new SearchSuggestionDto(
                    Type: "Category",
                    EntityId: category.Id,
                    Value: category.Name,
                    Label: category.Name,
                    Subtitle: category.NameEn)));
        }

        var ordered = candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TypeOrder)
            .ThenBy(x => x.Dto.Label, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Dto)
            .DistinctBy(BuildDedupKey, StringComparer.OrdinalIgnoreCase)
            .Take(normalizedLimit)
            .ToList();

        return Ok(ordered);
    }

    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<ProductDto>>> SearchProducts(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] bool promotedOnly = false,
        [FromQuery] decimal? ratingMin = null,
        [FromQuery] Guid? shopId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? city = null,
        [FromQuery] string? countryTag = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var query = new SearchQuery(
            Q: q,
            Page: page,
            PageSize: pageSize,
            MinPrice: minPrice,
            MaxPrice: maxPrice,
            InStockOnly: inStockOnly,
            PromotedOnly: promotedOnly,
            RatingMin: ratingMin,
            ShopId: shopId,
            CategoryId: categoryId,
            City: city,
            CountryTag: countryTag,
            Sort: sort);

        if (query.MinPrice.HasValue && query.MinPrice.Value < 0m)
        {
            return BadRequest("MinPrice invalide.");
        }

        if (query.MaxPrice.HasValue && query.MaxPrice.Value < 0m)
        {
            return BadRequest("MaxPrice invalide.");
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
        {
            return BadRequest("MinPrice ne peut pas etre superieur a MaxPrice.");
        }

        if (query.RatingMin.HasValue && (query.RatingMin.Value < 0m || query.RatingMin.Value > 5m))
        {
            return BadRequest("RatingMin doit etre entre 0 et 5.");
        }

        var normalizedPage = NormalizePage(query.Page);
        var normalizedPageSize = NormalizePageSize(query.PageSize);
        var queryResolution = await ResolveQueryRuleAsync(query.Q, ct);
        var normalizedTokens = SearchTextNormalizer.Tokenize(queryResolution.EffectiveQuery);
        var primaryToken = normalizedTokens.Count > 0 ? normalizedTokens[0] : null;
        var normalizedCity = NormalizeText(query.City, 80);
        var normalizedCityLower = SearchTextNormalizer.NormalizeLookup(normalizedCity, 80);
        var normalizedCountryTag = NormalizeCountryTag(query.CountryTag);
        var normalizedSort = NormalizeProductSort(query.Sort);
        var nowUtc = DateTime.UtcNow;
        var mappedCategoryId = queryResolution.TargetType == TargetTypeCategory ? queryResolution.TargetId : null;
        var mappedShopId = queryResolution.TargetType == TargetTypeShop ? queryResolution.TargetId : null;
        var mappedProductId = queryResolution.TargetType == TargetTypeProduct ? queryResolution.TargetId : null;

        IQueryable<Domain.Entities.Product> products = _db.Products.AsNoTracking()
            .Where(p => p.IsActive);

        if (query.ShopId.HasValue)
        {
            products = products.Where(p => p.ShopId == query.ShopId.Value);
        }
        else if (mappedShopId.HasValue)
        {
            var targetShopId = mappedShopId.Value;
            products = products.Where(p => p.ShopId == targetShopId);
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }
        else if (mappedCategoryId.HasValue)
        {
            var targetCategoryId = mappedCategoryId.Value;
            products = products.Where(p => p.CategoryId == targetCategoryId);
        }

        if (!string.IsNullOrWhiteSpace(normalizedCountryTag))
        {
            products = products.Where(p => p.Shop.CountryTag == normalizedCountryTag);
        }

        if (!string.IsNullOrWhiteSpace(normalizedCityLower))
        {
            products = products.Where(p => p.Shop.City.ToLower().Contains(normalizedCityLower));
        }

        if (query.InStockOnly)
        {
            products = products.Where(p => p.StockQty > 0);
        }

        if (query.PromotedOnly)
        {
            products = products.Where(p =>
                p.IsPromotionEnabled &&
                p.PromotionPrice.HasValue &&
                p.PromotionPrice > 0m &&
                p.PromotionPrice < p.Price &&
                (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc));
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(p =>
                (p.IsPromotionEnabled &&
                 p.PromotionPrice.HasValue &&
                 p.PromotionPrice > 0m &&
                 p.PromotionPrice < p.Price &&
                 (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                 (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)
                    ? p.PromotionPrice.Value
                    : p.Price) >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p =>
                (p.IsPromotionEnabled &&
                 p.PromotionPrice.HasValue &&
                 p.PromotionPrice > 0m &&
                 p.PromotionPrice < p.Price &&
                 (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                 (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)
                    ? p.PromotionPrice.Value
                    : p.Price) <= query.MaxPrice.Value);
        }

        if (query.RatingMin.HasValue)
        {
            var minRating = (double)query.RatingMin.Value;
            products = products.Where(p =>
                (_db.ShopReviews
                    .Where(r => r.ShopId == p.ShopId)
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0d) >= minRating);
        }

        if (normalizedTokens.Count > 0)
        {
            products = ApplyProductTextFilter(products, normalizedTokens);
        }

        products = normalizedSort switch
        {
            "price_asc" => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenBy(p =>
                    (double)(
                        p.IsPromotionEnabled &&
                        p.PromotionPrice.HasValue &&
                        p.PromotionPrice > 0m &&
                        p.PromotionPrice < p.Price &&
                        (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                        (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)
                            ? p.PromotionPrice.Value
                            : p.Price))
                .ThenBy(p => p.Name),
            "price_desc" => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenByDescending(p =>
                    (double)(
                        p.IsPromotionEnabled &&
                        p.PromotionPrice.HasValue &&
                        p.PromotionPrice > 0m &&
                        p.PromotionPrice < p.Price &&
                        (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                        (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)
                            ? p.PromotionPrice.Value
                            : p.Price))
                .ThenBy(p => p.Name),
            "rating_desc" => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenByDescending(p =>
                    _db.ShopReviews
                        .Where(r => r.ShopId == p.ShopId)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0d)
                .ThenByDescending(p => p.CreatedAtUtc),
            "newest" => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenByDescending(p => p.CreatedAtUtc),
            _ when !string.IsNullOrWhiteSpace(primaryToken) => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenByDescending(p => p.Name.ToLower() == primaryToken)
                .ThenByDescending(p => p.Name.ToLower().StartsWith(primaryToken))
                .ThenByDescending(p => p.Name.ToLower().Contains(primaryToken))
                .ThenByDescending(p =>
                    p.IsPromotionEnabled &&
                    p.PromotionPrice.HasValue &&
                    p.PromotionPrice > 0m &&
                    p.PromotionPrice < p.Price &&
                    (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                    (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc))
                .ThenByDescending(p => p.StockQty > 0)
                .ThenByDescending(p =>
                    _db.ShopReviews
                        .Where(r => r.ShopId == p.ShopId)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0d)
                .ThenBy(p => p.Name),
            _ => products
                .OrderByDescending(p => mappedProductId.HasValue && p.Id == mappedProductId.Value)
                .ThenByDescending(p => p.CreatedAtUtc)
        };

        var total = await products.CountAsync(ct);
        var pageRows = await products
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(p => new SearchProductRow(
                p.Id,
                p.ShopId,
                p.Shop.Name,
                p.Shop.City,
                p.Shop.CountryTag,
                p.CategoryId,
                p.Category.Name,
                p.Category.NameEn,
                p.Name,
                p.Description,
                p.Price,
                p.Currency,
                p.StockQty,
                p.MainImageUrl,
                p.CreatedAtUtc,
                p.IsPromotionEnabled,
                p.PromotionPrice,
                p.PromotionStartUtc,
                p.PromotionEndUtc,
                p.IsPromotionEnabled &&
                p.PromotionPrice.HasValue &&
                p.PromotionPrice > 0m &&
                p.PromotionPrice < p.Price &&
                (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc),
                p.IsPromotionEnabled &&
                p.PromotionPrice.HasValue &&
                p.PromotionPrice > 0m &&
                p.PromotionPrice < p.Price &&
                (!p.PromotionStartUtc.HasValue || p.PromotionStartUtc <= nowUtc) &&
                (!p.PromotionEndUtc.HasValue || p.PromotionEndUtc >= nowUtc)
                    ? p.PromotionPrice.Value
                    : p.Price,
                _db.ShopReviews
                    .Where(r => r.ShopId == p.ShopId)
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0d,
                _db.ShopReviews.Count(r => r.ShopId == p.ShopId)))
            .ToListAsync(ct);

        var items = pageRows.Select(x => new ProductDto(
            x.Id,
            x.ShopId,
            x.ShopName,
            x.CategoryId,
            x.CategoryName,
            x.CategoryNameEn,
            x.Name,
            x.Description,
            x.Price,
            x.EffectivePrice,
            x.HasActivePromotion,
            x.IsPromotionEnabled,
            x.PromotionPrice,
            ComputePromotionPercent(x.Price, x.PromotionPrice),
            x.PromotionStartUtc,
            x.PromotionEndUtc,
            x.Currency,
            x.StockQty,
            true,
            x.MainImageUrl,
            x.CreatedAtUtc,
            x.ShopRating <= 0d ? null : x.ShopRating,
            x.ShopReviewCount)).ToList();

        var pageResult = new PagedResult<ProductDto>
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            Total = total,
            Items = items
        };

        return Ok(pageResult.ToPublicUrls(Request));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<SearchCategoryDto>>> SearchCategories(
        [FromQuery] string? q = null,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] string? countryTag = null,
        CancellationToken ct = default)
    {
        var queryResolution = await ResolveQueryRuleAsync(q, ct);
        var normalizedTokens = SearchTextNormalizer.Tokenize(queryResolution.EffectiveQuery);
        var normalizedCountryTag = NormalizeCountryTag(countryTag);
        var normalizedLimit = NormalizeLimit(limit);
        var mappedCategoryId = queryResolution.TargetType == TargetTypeCategory ? queryResolution.TargetId : null;

        var categories = _db.Categories.AsNoTracking().AsQueryable();
        if (normalizedTokens.Count > 0)
        {
            categories = ApplyCategoryTextFilter(categories, normalizedTokens);
        }
        else if (mappedCategoryId.HasValue)
        {
            var targetCategoryId = mappedCategoryId.Value;
            categories = categories.Where(c => c.Id == targetCategoryId);
        }

        var activeProducts = _db.Products.AsNoTracking()
            .Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(normalizedCountryTag))
        {
            activeProducts = activeProducts.Where(p => p.Shop.CountryTag == normalizedCountryTag);
        }

        var projected = categories
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.NameEn,
                c.Slug,
                ActiveProductsCount = activeProducts.Count(p => p.CategoryId == c.Id),
                ActiveShopsCount = activeProducts
                    .Where(p => p.CategoryId == c.Id)
                    .Select(p => p.ShopId)
                    .Distinct()
                    .Count()
            });

        var items = await projected
            .Where(x => x.ActiveProductsCount > 0)
            .OrderByDescending(x => mappedCategoryId.HasValue && x.Id == mappedCategoryId.Value)
            .ThenByDescending(x => x.ActiveProductsCount)
            .ThenByDescending(x => x.ActiveShopsCount)
            .ThenBy(x => x.Name)
            .Take(normalizedLimit)
            .Select(x => new SearchCategoryDto(
                x.Id,
                x.Name,
                x.NameEn,
                x.Slug,
                x.ActiveProductsCount,
                x.ActiveShopsCount))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("shops")]
    public async Task<ActionResult<PagedResult<ShopDto>>> SearchShops(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] decimal? ratingMin = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool? recommended = null,
        [FromQuery] string? city = null,
        [FromQuery] string? countryTag = null,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        if (ratingMin.HasValue && (ratingMin.Value < 0m || ratingMin.Value > 5m))
        {
            return BadRequest("RatingMin doit etre entre 0 et 5.");
        }

        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var queryResolution = await ResolveQueryRuleAsync(q, ct);
        var normalizedTokens = SearchTextNormalizer.Tokenize(queryResolution.EffectiveQuery);
        var primaryToken = normalizedTokens.Count > 0 ? normalizedTokens[0] : null;
        var normalizedCity = NormalizeText(city, 80);
        var normalizedCityLower = SearchTextNormalizer.NormalizeLookup(normalizedCity, 80);
        var normalizedCountryTag = NormalizeCountryTag(countryTag);
        var normalizedSort = NormalizeShopSort(sort);
        var mappedShopId = queryResolution.TargetType == TargetTypeShop ? queryResolution.TargetId : null;
        var mappedCategoryId = queryResolution.TargetType == TargetTypeCategory ? queryResolution.TargetId : null;

        IQueryable<Domain.Entities.Shop> shops = _db.Shops.AsNoTracking();

        if (mappedShopId.HasValue)
        {
            var targetShopId = mappedShopId.Value;
            shops = shops.Where(s => s.Id == targetShopId);
        }

        if (verifiedOnly)
        {
            shops = shops.Where(s => s.IsVerified);
        }

        if (!string.IsNullOrWhiteSpace(normalizedCountryTag))
        {
            shops = shops.Where(s => s.CountryTag == normalizedCountryTag);
        }

        if (!string.IsNullOrWhiteSpace(normalizedCityLower))
        {
            shops = shops.Where(s => s.City.ToLower().Contains(normalizedCityLower));
        }

        if (categoryId.HasValue)
        {
            var filterCategoryId = categoryId.Value;
            shops = shops.Where(s =>
                _db.Products.Any(p =>
                    p.ShopId == s.Id &&
                    p.IsActive &&
                    p.CategoryId == filterCategoryId));
        }
        else if (mappedCategoryId.HasValue)
        {
            var filterCategoryId = mappedCategoryId.Value;
            shops = shops.Where(s =>
                _db.Products.Any(p =>
                    p.ShopId == s.Id &&
                    p.IsActive &&
                    p.CategoryId == filterCategoryId));
        }

        if (recommended == true)
        {
            shops = shops.Where(s => _db.Products.Any(p => p.ShopId == s.Id && p.IsActive));
        }

        if (ratingMin.HasValue)
        {
            var minRating = (double)ratingMin.Value;
            shops = shops.Where(s =>
                (_db.ShopReviews
                    .Where(r => r.ShopId == s.Id)
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0d) >= minRating);
        }

        if (normalizedTokens.Count > 0)
        {
            shops = ApplyShopTextFilter(shops, normalizedTokens);
        }

        shops = normalizedSort switch
        {
            "rating_desc" => shops
                .OrderByDescending(s => mappedShopId.HasValue && s.Id == mappedShopId.Value)
                .ThenByDescending(s =>
                    _db.ShopReviews
                        .Where(r => r.ShopId == s.Id)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0d)
                .ThenByDescending(s => s.IsVerified)
                .ThenBy(s => s.Name),
            "newest" => shops
                .OrderByDescending(s => mappedShopId.HasValue && s.Id == mappedShopId.Value)
                .ThenByDescending(s => s.CreatedAtUtc)
                .ThenBy(s => s.Name),
            "popular_desc" => shops
                .OrderByDescending(s => mappedShopId.HasValue && s.Id == mappedShopId.Value)
                .ThenByDescending(s => _db.OrderItems.Count(oi => oi.Product.ShopId == s.Id))
                .ThenByDescending(s => s.IsVerified)
                .ThenBy(s => s.Name),
            _ when !string.IsNullOrWhiteSpace(primaryToken) => shops
                .OrderByDescending(s => mappedShopId.HasValue && s.Id == mappedShopId.Value)
                .ThenByDescending(s => s.Name.ToLower() == primaryToken)
                .ThenByDescending(s => s.Name.ToLower().StartsWith(primaryToken))
                .ThenByDescending(s => s.Name.ToLower().Contains(primaryToken))
                .ThenByDescending(s => s.IsVerified)
                .ThenByDescending(s =>
                    _db.ShopReviews
                        .Where(r => r.ShopId == s.Id)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0d)
                .ThenBy(s => s.Name),
            _ => shops
                .OrderByDescending(s => mappedShopId.HasValue && s.Id == mappedShopId.Value)
                .ThenByDescending(s => s.IsVerified)
                .ThenByDescending(s => s.CreatedAtUtc)
                .ThenBy(s => s.Name)
        };

        var total = await shops.CountAsync(ct);
        var pageItems = await shops
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        var shopIds = pageItems.Select(s => s.Id).ToList();
        var ratings = await _db.ShopReviews.AsNoTracking()
            .Where(r => shopIds.Contains(r.ShopId))
            .GroupBy(r => r.ShopId)
            .Select(g => new ShopRatingSummary(
                g.Key,
                g.Average(r => (double)r.Rating),
                g.Count()))
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
                hasRating ? rating!.ReviewCount : 0);
        }).ToList();

        var pageResult = new PagedResult<ShopDto>
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            Total = total,
            Items = items
        };

        return Ok(ToPublic(pageResult, Request));
    }

    private async Task<SearchQueryResolution> ResolveQueryRuleAsync(string? q, CancellationToken ct)
    {
        var normalizedUserQuery = NormalizeQuery(q);
        if (string.IsNullOrWhiteSpace(normalizedUserQuery))
        {
            return new SearchQueryResolution(
                UserQuery: null,
                EffectiveQuery: null,
                EffectiveLower: null,
                TargetType: null,
                TargetId: null);
        }

        var triggerLower = SearchTextNormalizer.NormalizeLookup(normalizedUserQuery, 120) ?? normalizedUserQuery.ToLowerInvariant();
        var rule = await _db.SearchQueryRules.AsNoTracking()
            .Where(x => x.IsActive && x.TriggerQuery == triggerLower)
            .Select(x => new RuleProjection(
                x.CanonicalQuery,
                x.TargetType,
                x.TargetId))
            .FirstOrDefaultAsync(ct);

        if (rule is null)
        {
            return new SearchQueryResolution(
                UserQuery: normalizedUserQuery,
                EffectiveQuery: normalizedUserQuery,
                EffectiveLower: SearchTextNormalizer.NormalizeLookup(normalizedUserQuery, 120),
                TargetType: null,
                TargetId: null);
        }

        var normalizedTargetType = NormalizeTargetType(rule.TargetType);
        var targetId = normalizedTargetType is null ? null : rule.TargetId;
        var canonicalQuery = NormalizeQuery(rule.CanonicalQuery);
        var effectiveQuery = canonicalQuery;

        if (string.IsNullOrWhiteSpace(effectiveQuery))
        {
            // Direct target mapping can work without text filtering.
            effectiveQuery = targetId.HasValue ? null : normalizedUserQuery;
        }

        return new SearchQueryResolution(
            UserQuery: normalizedUserQuery,
            EffectiveQuery: effectiveQuery,
            EffectiveLower: SearchTextNormalizer.NormalizeLookup(effectiveQuery, 120),
            TargetType: normalizedTargetType,
            TargetId: targetId);
    }

    private async Task<SuggestionCandidate?> BuildMappedSuggestionCandidateAsync(SearchQueryResolution resolution, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resolution.TargetType) || !resolution.TargetId.HasValue)
        {
            return null;
        }

        SearchSuggestionDto? dto = null;
        if (resolution.TargetType == TargetTypeProduct)
        {
            dto = await _db.Products.AsNoTracking()
                .Where(x => x.Id == resolution.TargetId.Value && x.IsActive)
                .Select(x => new SearchSuggestionDto(
                    TargetTypeProduct,
                    x.Id,
                    x.Name,
                    x.Name,
                    x.Shop.Name))
                .FirstOrDefaultAsync(ct);
        }
        else if (resolution.TargetType == TargetTypeShop)
        {
            dto = await _db.Shops.AsNoTracking()
                .Where(x => x.Id == resolution.TargetId.Value)
                .Select(x => new SearchSuggestionDto(
                    TargetTypeShop,
                    x.Id,
                    x.Name,
                    x.Name,
                    x.City + ", " + x.CountryTag))
                .FirstOrDefaultAsync(ct);
        }
        else if (resolution.TargetType == TargetTypeCategory)
        {
            dto = await _db.Categories.AsNoTracking()
                .Where(x => x.Id == resolution.TargetId.Value)
                .Select(x => new SearchSuggestionDto(
                    TargetTypeCategory,
                    x.Id,
                    x.Name,
                    x.Name,
                    x.NameEn))
                .FirstOrDefaultAsync(ct);
        }

        if (dto is null)
        {
            return null;
        }

        var score = 500;
        if (!string.IsNullOrWhiteSpace(resolution.UserQuery))
        {
            score += ScoreLabel(dto.Label, resolution.UserQuery);
        }

        return new SuggestionCandidate(score, -1, dto);
    }

    private static string? NormalizeQuery(string? q)
        => SearchTextNormalizer.NormalizeWords(q, 80);

    private static string? NormalizeTargetType(string? value)
    {
        var normalized = NormalizeText(value, 20);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, TargetTypeProduct, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeProduct;
        }

        if (string.Equals(normalized, TargetTypeShop, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeShop;
        }

        if (string.Equals(normalized, TargetTypeCategory, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeCategory;
        }

        return null;
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultLimit;
        }

        return limit > MaxLimit ? MaxLimit : limit;
    }

    private static int NormalizePage(int page)
        => page <= 0 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return DefaultPageSize;
        }

        return pageSize > MaxPageSize ? MaxPageSize : pageSize;
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeCountryTag(string? countryTag)
        => NormalizeText(countryTag, 8)?.ToUpperInvariant();

    private static string NormalizeProductSort(string? sort)
    {
        var normalized = (sort ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "price_asc" => "price_asc",
            "priceasc" => "price_asc",
            "price_desc" => "price_desc",
            "pricedesc" => "price_desc",
            "rating_desc" => "rating_desc",
            "rating" => "rating_desc",
            "newest" => "newest",
            _ => "relevance"
        };
    }

    private static string NormalizeShopSort(string? sort)
    {
        var normalized = (sort ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "rating_desc" => "rating_desc",
            "rating" => "rating_desc",
            "newest" => "newest",
            "popular_desc" => "popular_desc",
            "popular" => "popular_desc",
            _ => "relevance"
        };
    }

    private static IQueryable<Domain.Entities.Product> ApplyProductTextFilter(
        IQueryable<Domain.Entities.Product> query,
        IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var local = token;
            query = query.Where(p =>
                p.Name.ToLower().Contains(local) ||
                (p.Description != null && p.Description.ToLower().Contains(local)) ||
                p.Shop.Name.ToLower().Contains(local) ||
                p.Shop.City.ToLower().Contains(local) ||
                p.Category.Name.ToLower().Contains(local) ||
                (p.Category.NameEn != null && p.Category.NameEn.ToLower().Contains(local)));
        }

        return query;
    }

    private static IQueryable<Domain.Entities.Shop> ApplyShopTextFilter(
        IQueryable<Domain.Entities.Shop> query,
        IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var local = token;
            query = query.Where(s =>
                s.Name.ToLower().Contains(local) ||
                s.City.ToLower().Contains(local) ||
                s.CountryTag.ToLower().Contains(local));
        }

        return query;
    }

    private static IQueryable<Domain.Entities.Category> ApplyCategoryTextFilter(
        IQueryable<Domain.Entities.Category> query,
        IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var local = token;
            query = query.Where(c =>
                c.Name.ToLower().Contains(local) ||
                (c.NameEn != null && c.NameEn.ToLower().Contains(local)));
        }

        return query;
    }

    private async Task<IReadOnlyList<PopularQueryRow>> GetPopularQueriesAsync(
        IReadOnlyList<string> tokens,
        CancellationToken ct)
    {
        if (tokens.Count == 0)
        {
            return Array.Empty<PopularQueryRow>();
        }

        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var grouped = await _db.SearchAnalyticsEvents.AsNoTracking()
            .Where(x => x.EventType == SearchAnalyticsEvents.Query)
            .Where(x => x.OccurredAtUtc >= fromUtc)
            .Where(x => x.NormalizedQuery != null && x.NormalizedQuery != string.Empty)
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                Query = g.Key,
                Searches = g.Count(),
                LastSeenAtUtc = g.Max(x => x.OccurredAtUtc)
            })
            .ToListAsync(ct);

        var rows = grouped
            .Select(x => new PopularQueryRow(x.Query, x.Searches, x.LastSeenAtUtc))
            .OrderByDescending(x => x.Searches)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .Take(PopularQueryCandidatePool)
            .ToList();

        return rows
            .Where(x => SearchTextNormalizer.ContainsAllTokens(x.Query, tokens))
            .Take(8)
            .ToList();
    }

    private static int ScoreLabel(string? label, string normalizedQuery)
    {
        var normalizedLabel = SearchTextNormalizer.NormalizeLookup(label, 160);
        var normalizedTarget = SearchTextNormalizer.NormalizeLookup(normalizedQuery, 120);
        if (string.IsNullOrWhiteSpace(normalizedLabel) || string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return 0;
        }

        if (string.Equals(normalizedLabel, normalizedTarget, StringComparison.Ordinal))
        {
            return 300;
        }

        if (normalizedLabel.StartsWith(normalizedTarget, StringComparison.Ordinal))
        {
            return 200;
        }

        if (normalizedLabel.Contains(normalizedTarget, StringComparison.Ordinal))
        {
            return 100;
        }

        return 0;
    }

    private static string BuildDedupKey(SearchSuggestionDto dto)
        => $"{dto.Type}|{dto.EntityId?.ToString() ?? string.Empty}|{dto.Value}";

    private static decimal? ComputePromotionPercent(decimal price, decimal? promotionPrice)
    {
        if (!promotionPrice.HasValue || price <= 0m)
        {
            return null;
        }

        if (promotionPrice.Value <= 0m || promotionPrice.Value >= price)
        {
            return null;
        }

        var ratio = (price - promotionPrice.Value) / price;
        return decimal.Round(ratio * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static ShopDto ToPublic(ShopDto dto, HttpRequest request)
        => dto with { ImageUrl = ResponseUrlNormalizer.ToAbsoluteUrl(dto.ImageUrl, request) };

    private static PagedResult<ShopDto> ToPublic(PagedResult<ShopDto> page, HttpRequest request)
        => new()
        {
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
            Items = page.Items.Select(x => ToPublic(x, request)).ToList()
        };

    private sealed record ProductSuggestionRow(
        Guid Id,
        string Name,
        string ShopName,
        bool HasActivePromotion);

    private sealed record ShopSuggestionRow(
        Guid Id,
        string Name,
        string City,
        string CountryTag,
        bool IsVerified);

    private sealed record CategorySuggestionRow(
        Guid Id,
        string Name,
        string? NameEn);

    private sealed record SearchProductRow(
        Guid Id,
        Guid ShopId,
        string ShopName,
        string ShopCity,
        string ShopCountryTag,
        Guid CategoryId,
        string CategoryName,
        string? CategoryNameEn,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        int StockQty,
        string? MainImageUrl,
        DateTime CreatedAtUtc,
        bool IsPromotionEnabled,
        decimal? PromotionPrice,
        DateTime? PromotionStartUtc,
        DateTime? PromotionEndUtc,
        bool HasActivePromotion,
        decimal EffectivePrice,
        double ShopRating,
        int ShopReviewCount);

    private sealed record ShopRatingSummary(
        Guid ShopId,
        double Rating,
        int ReviewCount);

    private sealed record PopularQueryRow(
        string Query,
        int Searches,
        DateTime LastSeenAtUtc);

    private sealed record SuggestionCandidate(
        int Score,
        int TypeOrder,
        SearchSuggestionDto Dto);

    private sealed record SearchQueryResolution(
        string? UserQuery,
        string? EffectiveQuery,
        string? EffectiveLower,
        string? TargetType,
        Guid? TargetId);

    private sealed record RuleProjection(
        string? CanonicalQuery,
        string? TargetType,
        Guid? TargetId);
}

