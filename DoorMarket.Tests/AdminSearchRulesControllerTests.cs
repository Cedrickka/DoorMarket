using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DoorMarket.Tests;

public class AdminSearchRulesControllerTests
{
    [Fact]
    public async Task CreateRule_WithCanonicalOnly_CreatesRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "mixeur",
                CanonicalQuery: "blender",
                TargetType: null,
                TargetId: null,
                IsActive: true,
                Note: "Synonyme FR -> EN"),
            CancellationToken.None);

        var dto = AssertOk(result);
        Assert.Equal("mixeur", dto.TriggerQuery);
        Assert.Equal("blender", dto.CanonicalQuery);
        Assert.Null(dto.TargetType);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateRule_WithInvalidTarget_ReturnsBadRequest()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "promo coin",
                CanonicalQuery: null,
                TargetType: "Shop",
                TargetId: Guid.NewGuid(),
                IsActive: true,
                Note: null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateRule_WithCategoryTarget_UpdatesRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "electro",
                CanonicalQuery: null,
                TargetType: "Category",
                TargetId: fixture.CategoryId,
                IsActive: true,
                Note: null),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        var updated = await controller.UpdateRule(
            createdDto.Id,
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "electro",
                CanonicalQuery: "appliances",
                TargetType: "Category",
                TargetId: fixture.CategoryId,
                IsActive: false,
                Note: "Paused"),
            CancellationToken.None);
        var updatedDto = AssertOk(updated);

        Assert.Equal("appliances", updatedDto.CanonicalQuery);
        Assert.Equal("Category", updatedDto.TargetType);
        Assert.False(updatedDto.IsActive);
        Assert.Equal("Paused", updatedDto.Note);
    }

    [Fact]
    public async Task GetSuggestions_WithNoResultTypo_ReturnsSuggestedProductRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        fixture.Db.SearchAnalyticsEvents.Add(new SearchAnalyticsEvent
        {
            EventType = SearchAnalyticsEvents.Query,
            Query = "blenderr",
            NormalizedQuery = "blenderr",
            ResultsCount = 0,
            Source = "Web",
            OccurredAtUtc = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.GetSuggestions(
            from: DateTime.UtcNow.AddDays(-7),
            to: DateTime.UtcNow.AddDays(1),
            source: "web",
            take: 10,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<AdminSearchRulesController.SearchRuleSuggestionDto>>(ok.Value);
        var suggestion = Assert.Single(rows);
        Assert.Equal("blenderr", suggestion.TriggerQuery);
        Assert.Equal("Product", suggestion.TargetType);
        Assert.Equal(fixture.ProductId, suggestion.TargetId);
        Assert.Equal("blender", suggestion.SuggestedCanonicalQuery);
    }

    [Fact]
    public async Task ApplySuggestions_WithQualityFilters_CreatesRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.ApplySuggestions(
            new AdminSearchRulesController.ApplySuggestionsRequestDto(
                From: DateTime.UtcNow.AddDays(-7),
                To: DateTime.UtcNow.AddDays(1),
                Source: "web",
                Take: 20,
                MinScore: 70,
                MinNoResultCount: 2,
                ConfidenceFilter: "Any",
                IsActive: true,
                NotePrefix: "Batch auto"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminSearchRulesController.ApplySuggestionsResponseDto>(ok.Value);
        Assert.Equal(1, payload.TotalSuggestions);
        Assert.Equal(1, payload.EligibleSuggestions);
        Assert.Equal(1, payload.Created);
        Assert.Equal(0, payload.SkippedExisting);

        var created = await fixture.Db.SearchQueryRules.AsNoTracking().SingleAsync();
        Assert.Equal("blenderr", created.TriggerQuery);
        Assert.Equal("Product", created.TargetType);
        Assert.Equal(fixture.ProductId, created.TargetId);
    }

    [Fact]
    public async Task GetEffectiveness_WithEvents_ReturnsComputedMetrics()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: true,
                Note: "test"),
            CancellationToken.None);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.GetEffectiveness(
            from: DateTime.UtcNow.AddDays(-7),
            to: DateTime.UtcNow.AddDays(1),
            source: "web",
            q: "blenderr",
            active: true,
            take: 20,
            problemOnly: false,
            minQueries: 1,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<AdminSearchRulesController.SearchRuleEffectivenessDto>>(ok.Value);
        var row = Assert.Single(rows);
        Assert.Equal("blenderr", row.TriggerQuery);
        Assert.Equal(2, row.QueryCount);
        Assert.Equal(1, row.NoResultCount);
        Assert.Equal(1, row.ClickCount);
        Assert.Equal(50m, row.NoResultRatePct);
        Assert.Equal(50m, row.CtrPct);
        Assert.Equal("NeedsAttention", row.Performance);
    }

    [Fact]
    public async Task DeactivateProblematicRules_WithLowQualityRule_DeactivatesRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: true,
                Note: "before"),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.DeactivateProblematicRules(
            new AdminSearchRulesController.DeactivateProblematicRulesRequestDto(
                From: DateTime.UtcNow.AddDays(-7),
                To: DateTime.UtcNow.AddDays(1),
                Source: "web",
                Q: "blenderr",
                Active: true,
                Take: 20,
                MinQueries: 3,
                MinNoResultRatePct: 50m,
                MaxCtrPct: 5m,
                NoteSuffix: "Auto-disabled test"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminSearchRulesController.DeactivateProblematicRulesResponseDto>(ok.Value);
        Assert.Equal(1, payload.EvaluatedRules);
        Assert.Equal(1, payload.MatchedProblematicRules);
        Assert.Equal(1, payload.DeactivatedRules);
        Assert.Contains("blenderr", payload.TriggerQueries);

        var row = await fixture.Db.SearchQueryRules.AsNoTracking().SingleAsync(x => x.Id == createdDto.Id);
        Assert.False(row.IsActive);
        Assert.Contains("Auto-disabled test", row.Note);
    }

    [Fact]
    public async Task DeactivateProblematicRules_DryRun_DoesNotChangeRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: true,
                Note: "before"),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.DeactivateProblematicRules(
            new AdminSearchRulesController.DeactivateProblematicRulesRequestDto(
                From: DateTime.UtcNow.AddDays(-7),
                To: DateTime.UtcNow.AddDays(1),
                Source: "web",
                Q: "blenderr",
                Active: true,
                Take: 20,
                MinQueries: 3,
                MinNoResultRatePct: 50m,
                MaxCtrPct: 5m,
                NoteSuffix: "dry-run",
                DryRun: true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminSearchRulesController.DeactivateProblematicRulesResponseDto>(ok.Value);
        Assert.True(payload.DryRun);
        Assert.Equal(0, payload.DeactivatedRules);
        Assert.Equal(1, payload.WouldDeactivateRules);

        var row = await fixture.Db.SearchQueryRules.AsNoTracking().SingleAsync(x => x.Id == createdDto.Id);
        Assert.True(row.IsActive);
        Assert.Equal("before", row.Note);
    }

    [Fact]
    public async Task ExportEffectivenessCsv_WithData_ReturnsCsvFile()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: true,
                Note: "csv"),
            CancellationToken.None);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 0,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 4,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.ExportEffectivenessCsv(
            from: DateTime.UtcNow.AddDays(-7),
            to: DateTime.UtcNow.AddDays(1),
            source: "web",
            q: "blenderr",
            active: true,
            take: 100,
            problemOnly: false,
            minQueries: 1,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);

        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Id,TriggerQuery,CanonicalQuery,TargetType,TargetId,IsActive,Performance,QueryCount,NoResultCount,ClickCount,NoResultRatePct,CtrPct,LastSeenAtUtc", csv, StringComparison.Ordinal);
        Assert.Contains("\"blenderr\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"NeedsAttention\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReactivateRecoveredRules_WithRecoveredInactiveRule_ReactivatesRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: false,
                Note: "disabled"),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 6,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-4)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 4,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 3,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 2,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(-30)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.ReactivateRecoveredRules(
            new AdminSearchRulesController.ReactivateRecoveredRulesRequestDto(
                From: DateTime.UtcNow.AddDays(-7),
                To: DateTime.UtcNow.AddDays(1),
                Source: "web",
                Q: "blenderr",
                Active: false,
                Take: 20,
                MinQueries: 5,
                MaxNoResultRatePct: 10m,
                MinCtrPct: 20m,
                NoteSuffix: "Auto-reactivated test"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminSearchRulesController.ReactivateRecoveredRulesResponseDto>(ok.Value);
        Assert.Equal(1, payload.EvaluatedRules);
        Assert.Equal(1, payload.MatchedRecoveredRules);
        Assert.Equal(1, payload.ReactivatedRules);
        Assert.Contains("blenderr", payload.TriggerQueries);

        var row = await fixture.Db.SearchQueryRules.AsNoTracking().SingleAsync(x => x.Id == createdDto.Id);
        Assert.True(row.IsActive);
        Assert.Contains("Auto-reactivated test", row.Note);
    }

    [Fact]
    public async Task ReactivateRecoveredRules_DryRun_DoesNotChangeRule()
    {
        await using var fixture = await SearchRulesFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreateRule(
            new AdminSearchRulesController.UpsertSearchRuleRequest(
                TriggerQuery: "blenderr",
                CanonicalQuery: "blender",
                TargetType: "Product",
                TargetId: fixture.ProductId,
                IsActive: false,
                Note: "disabled"),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        fixture.Db.SearchAnalyticsEvents.AddRange(
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-4)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Query,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                ResultsCount = 5,
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow.AddSeconds(-30)
            },
            new SearchAnalyticsEvent
            {
                EventType = SearchAnalyticsEvents.Click,
                Query = "blenderr",
                NormalizedQuery = "blenderr",
                Source = "Web",
                OccurredAtUtc = DateTime.UtcNow
            });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.ReactivateRecoveredRules(
            new AdminSearchRulesController.ReactivateRecoveredRulesRequestDto(
                From: DateTime.UtcNow.AddDays(-7),
                To: DateTime.UtcNow.AddDays(1),
                Source: "web",
                Q: "blenderr",
                Active: false,
                Take: 20,
                MinQueries: 5,
                MaxNoResultRatePct: 10m,
                MinCtrPct: 20m,
                NoteSuffix: "dry-run",
                DryRun: true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminSearchRulesController.ReactivateRecoveredRulesResponseDto>(ok.Value);
        Assert.True(payload.DryRun);
        Assert.Equal(0, payload.ReactivatedRules);
        Assert.Equal(1, payload.WouldReactivateRules);

        var row = await fixture.Db.SearchQueryRules.AsNoTracking().SingleAsync(x => x.Id == createdDto.Id);
        Assert.False(row.IsActive);
        Assert.Equal("disabled", row.Note);
    }

    private static AdminSearchRulesController.SearchRuleDto AssertOk(
        ActionResult<AdminSearchRulesController.SearchRuleDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AdminSearchRulesController.SearchRuleDto>(ok.Value);
    }

    private sealed class SearchRulesFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SearchRulesFixture(SqliteConnection connection, DoorMarketDbContext db, Guid categoryId, Guid productId)
        {
            _connection = connection;
            Db = db;
            CategoryId = categoryId;
            ProductId = productId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid CategoryId { get; }
        public Guid ProductId { get; }

        public AdminSearchRulesController CreateController() => new(Db, new SearchRuleSuggestionService(Db));

        public static async Task<SearchRulesFixture> CreateAsync()
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
                Email = "owner-rules@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Electronics",
                Slug = "electronics"
            };

            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "Tech Corner",
                CountryTag = "CD",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Blender",
                Price = 35m,
                Currency = "USD",
                StockQty = 7,
                IsActive = true
            };

            db.Products.Add(product);

            await db.SaveChangesAsync();

            return new SearchRulesFixture(connection, db, category.Id, product.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
