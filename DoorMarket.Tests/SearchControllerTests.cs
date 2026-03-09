using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.DTOs.Search;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class SearchControllerTests
{
    [Fact]
    public async Task GetSuggestions_WithPrefixAndContains_RanksPrefixFirst()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.GetSuggestions("app", 12, CancellationToken.None);
        var payload = AssertOk(result);

        var productSuggestions = payload
            .Where(x => string.Equals(x.Type, "Product", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(productSuggestions);

        var prefixIndex = productSuggestions.FindIndex(x => x.Label == "Apple Juice");
        var containsIndex = productSuggestions.FindIndex(x => x.Label == "Pineapple Snack");

        Assert.True(prefixIndex >= 0);
        Assert.True(containsIndex >= 0);
        Assert.True(prefixIndex < containsIndex);
        Assert.Contains(payload, x => x.Type == "Shop");
        Assert.Contains(payload, x => x.Type == "Category");
    }

    [Fact]
    public async Task GetSuggestions_WithShortQuery_ReturnsEmptyList()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.GetSuggestions("a", 12, CancellationToken.None);
        var payload = AssertOk(result);

        Assert.Empty(payload);
    }

    [Fact]
    public async Task GetSuggestions_WithLimitAboveMax_CapsAtTwentyFive()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        await fixture.AddAlphaProductsAsync(40);
        var controller = fixture.CreateController();

        var result = await controller.GetSuggestions("alpha", 100, CancellationToken.None);
        var payload = AssertOk(result);

        Assert.True(payload.Count <= 25);
    }

    [Fact]
    public async Task GetSuggestions_WithPopularQueries_IncludesQuerySuggestion()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "apple deals",
                NormalizedQuery = "apple deals",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "apple deals",
                NormalizedQuery = "apple deals",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.GetSuggestions("apple", 12, CancellationToken.None);
        var payload = AssertOk(result);

        Assert.Contains(payload, x =>
            string.Equals(x.Type, "Query", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Value, "apple deals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchProducts_WithInStockAndPromotedOnly_ReturnsOnlyActivePromotedInStockRows()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchProducts(
            q: "app",
            inStockOnly: true,
            promotedOnly: true,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, item =>
        {
            Assert.True(item.StockQty > 0);
            Assert.True(item.HasActivePromotion);
            Assert.True(item.IsActive);
        });
        Assert.Contains(payload.Items, x => x.Name == "Apple Juice");
        Assert.DoesNotContain(payload.Items, x => x.Name == "App Roll");
    }

    [Fact]
    public async Task SearchProducts_WithPriceAscSort_ReturnsAscendingEffectivePrice()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchProducts(
            sort: "price_asc",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.True(payload.Items.Count >= 2);
        Assert.True(payload.Items[0].EffectivePrice <= payload.Items[1].EffectivePrice);
    }

    [Fact]
    public async Task SearchProducts_WithRatingMin_FiltersByShopRating()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchProducts(
            ratingMin: 4m,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, item => Assert.Equal("App Market", item.ShopName));
        Assert.DoesNotContain(payload.Items, item => item.ShopName == "Corner Shop");
    }

    [Fact]
    public async Task SearchProducts_WithCanonicalRule_UsesCanonicalQuery()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        fixture.Db.SearchQueryRules.Add(new SearchQueryRule
        {
            TriggerQuery = "mixeur",
            CanonicalQuery = "blender",
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchProducts(
            q: "mixeur",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.Contains(payload.Items, x => x.Name == "Blender Max");
    }

    [Fact]
    public async Task SearchProducts_WithAccentInQuery_MatchesFoldedTokens()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        fixture.Db.Products.Add(new Product
        {
            ShopId = fixture.ShopId,
            CategoryId = fixture.CategoryId,
            Name = "Cafe Press",
            Price = 12m,
            Currency = "USD",
            StockQty = 5,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.SearchProducts(
            q: "café",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.Contains(payload.Items, x => x.Name == "Cafe Press");
    }

    [Fact]
    public async Task SearchProducts_Relevance_UsesRatingBoostWhenTextSignalsAreEqual()
    {
        await using var fixture = await SearchFixture.CreateAsync();

        fixture.Db.Products.AddRange(
            new Product
            {
                ShopId = fixture.ShopId,
                CategoryId = fixture.CategoryId,
                Name = "Apple Item Prime",
                Price = 10m,
                Currency = "USD",
                StockQty = 8,
                IsActive = true
            },
            new Product
            {
                ShopId = fixture.ShopTwoId,
                CategoryId = fixture.CategoryId,
                Name = "Apple Item Prime Plus",
                Price = 10m,
                Currency = "USD",
                StockQty = 8,
                IsActive = true
            });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.SearchProducts(
            q: "apple item prime",
            sort: "relevance",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        var items = payload.Items.ToList();
        var first = Assert.Single(items, x => x.Name == "Apple Item Prime");
        var second = Assert.Single(items, x => x.Name == "Apple Item Prime Plus");
        var firstIndex = items.IndexOf(first);
        var secondIndex = items.IndexOf(second);

        Assert.True(firstIndex >= 0);
        Assert.True(secondIndex >= 0);
        Assert.True(firstIndex < secondIndex);
    }

    [Fact]
    public async Task SearchProducts_WithCategoryTargetRuleWithoutCanonical_ReturnsMappedCategoryProducts()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        fixture.Db.SearchQueryRules.Add(new SearchQueryRule
        {
            TriggerQuery = "electro",
            CanonicalQuery = null,
            TargetType = "Category",
            TargetId = fixture.AppliancesCategoryId,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchProducts(
            q: "electro",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, x => Assert.Equal(fixture.AppliancesCategoryId, x.CategoryId));
        Assert.Contains(payload.Items, x => x.Name == "Blender Max");
    }

    [Fact]
    public async Task GetSuggestions_WithMappedTargetWithoutCanonical_PrioritizesMappedSuggestion()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        fixture.Db.SearchQueryRules.Add(new SearchQueryRule
        {
            TriggerQuery = "coin",
            CanonicalQuery = null,
            TargetType = "Shop",
            TargetId = fixture.ShopTwoId,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();
        var controller = fixture.CreateController();

        var result = await controller.GetSuggestions("coin", 12, CancellationToken.None);
        var payload = AssertOk(result);

        Assert.NotEmpty(payload);
        Assert.Equal("Shop", payload[0].Type);
        Assert.Equal(fixture.ShopTwoId, payload[0].EntityId);
    }

    [Fact]
    public async Task SearchShops_WithVerifiedOnly_ReturnsOnlyVerifiedShops()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchShops(
            verifiedOnly: true,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, shop => Assert.True(shop.IsVerified));
        Assert.Contains(payload.Items, shop => shop.Name == "App Market");
        Assert.DoesNotContain(payload.Items, shop => shop.Name == "Corner Shop");
    }

    [Fact]
    public async Task SearchShops_WithRatingMin_FiltersShops()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchShops(
            ratingMin: 4m,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, shop => Assert.Equal("App Market", shop.Name));
        Assert.DoesNotContain(payload.Items, shop => shop.Name == "Corner Shop");
    }

    [Fact]
    public async Task SearchShops_WithCityAndCountryTagFilters_ReturnsMatchingShops()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchShops(
            city: "lub",
            countryTag: "cd",
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.Single(payload.Items);
        Assert.Equal("Corner Shop", payload.Items[0].Name);
    }

    [Fact]
    public async Task SearchCategories_WithQuery_ReturnsMatchingCategoryWithCounts()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchCategories(
            q: "appl",
            limit: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.Single(payload);
        Assert.Equal("Appliances", payload[0].Name);
        Assert.True(payload[0].ActiveProductsCount >= 1);
    }

    [Fact]
    public async Task SearchCategories_WithoutQuery_ReturnsMostActiveCategoryFirst()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.SearchCategories(
            limit: 20,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.True(payload.Count >= 2);
        Assert.Equal("Food", payload[0].Name);
        Assert.True(payload[0].ActiveProductsCount >= payload[1].ActiveProductsCount);
    }

    private static IReadOnlyList<SearchSuggestionDto> AssertOk(ActionResult<IReadOnlyList<SearchSuggestionDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IReadOnlyList<SearchSuggestionDto>>(ok.Value);
    }

    private static IReadOnlyList<SearchCategoryDto> AssertOk(ActionResult<IReadOnlyList<SearchCategoryDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IReadOnlyList<SearchCategoryDto>>(ok.Value);
    }

    private static PagedResult<ProductDto> AssertOk(ActionResult<PagedResult<ProductDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedResult<ProductDto>>(ok.Value);
    }

    private static PagedResult<ShopDto> AssertOk(ActionResult<PagedResult<ShopDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedResult<ShopDto>>(ok.Value);
    }

    private sealed class SearchFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SearchFixture(SqliteConnection connection, DoorMarketDbContext db, Guid shopId, Guid categoryId, Guid appliancesCategoryId, Guid shopTwoId)
        {
            _connection = connection;
            Db = db;
            ShopId = shopId;
            CategoryId = categoryId;
            AppliancesCategoryId = appliancesCategoryId;
            ShopTwoId = shopTwoId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ShopId { get; }
        public Guid CategoryId { get; }
        public Guid AppliancesCategoryId { get; }
        public Guid ShopTwoId { get; }

        public SearchController CreateController() => new(Db);

        public async Task AddAlphaProductsAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Db.Products.Add(new Product
                {
                    ShopId = ShopId,
                    CategoryId = CategoryId,
                    Name = $"Alpha Product {i + 1}",
                    Price = 20m + i,
                    Currency = "USD",
                    StockQty = 5,
                    IsActive = true
                });
            }

            await Db.SaveChangesAsync();
        }

        public static async Task<SearchFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var owner = new User
            {
                Email = "owner-search@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var ownerTwo = new User
            {
                Email = "owner-search-2@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var reviewerOne = new User
            {
                Email = "reviewer-1@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var reviewerTwo = new User
            {
                Email = "reviewer-2@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var category = new Category
            {
                Name = "Appliances",
                NameEn = "Appliances",
                Slug = "appliances"
            };

            var categoryOther = new Category
            {
                Name = "Food",
                NameEn = "Food",
                Slug = "food"
            };

            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "App Market",
                CountryTag = "CD",
                City = "Kinshasa",
                IsVerified = true
            };

            var shopTwo = new Shop
            {
                OwnerUser = ownerTwo,
                Name = "Corner Shop",
                CountryTag = "CD",
                City = "Lubumbashi",
                IsVerified = false
            };

            var now = DateTime.UtcNow;
            db.Products.AddRange(
                new Product
                {
                    Shop = shop,
                    Category = categoryOther,
                    Name = "Apple Juice",
                    Price = 10m,
                    Currency = "USD",
                    StockQty = 20,
                    IsActive = true,
                    IsPromotionEnabled = true,
                    PromotionPrice = 8m,
                    PromotionStartUtc = now.AddDays(-1),
                    PromotionEndUtc = now.AddDays(1)
                },
                new Product
                {
                    Shop = shop,
                    Category = categoryOther,
                    Name = "Pineapple Snack",
                    Price = 9m,
                    Currency = "USD",
                    StockQty = 10,
                    IsActive = true
                },
                new Product
                {
                    Shop = shopTwo,
                    Category = categoryOther,
                    Name = "App Roll",
                    Price = 7m,
                    Currency = "USD",
                    StockQty = 0,
                    IsActive = true
                },
                new Product
                {
                    Shop = shop,
                    Category = category,
                    Name = "Blender Max",
                    Price = 30m,
                    Currency = "USD",
                    StockQty = 3,
                    IsActive = true
                },
                new Product
                {
                    Shop = shop,
                    Category = categoryOther,
                    Name = "App Hidden",
                    Price = 11m,
                    Currency = "USD",
                    StockQty = 10,
                    IsActive = false
                });

            db.ShopReviews.AddRange(
                new ShopReview
                {
                    Shop = shop,
                    User = reviewerOne,
                    Rating = 5,
                    Comment = "Excellent"
                },
                new ShopReview
                {
                    Shop = shopTwo,
                    User = reviewerTwo,
                    Rating = 2,
                    Comment = "Average"
                });

            db.AddRange(owner, ownerTwo, reviewerOne, reviewerTwo, category, categoryOther, shop, shopTwo);
            await db.SaveChangesAsync();

            return new SearchFixture(connection, db, shop.Id, categoryOther.Id, category.Id, shopTwo.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
