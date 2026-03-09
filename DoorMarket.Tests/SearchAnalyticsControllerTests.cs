using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class SearchAnalyticsControllerTests
{
    [Fact]
    public async Task TrackQuery_PersistsNormalizedEvent()
    {
        await using var fixture = await AnalyticsFixture.CreateAsync();
        var controller = fixture.CreateTrackingController();

        var result = await controller.TrackQuery(
            new SearchAnalyticsController.TrackSearchQueryRequest(
                Query: "  Apple   Juice  ",
                ResultsCount: -7,
                DurationMs: 145,
                Page: 1,
                Sort: "relevance",
                FiltersHash: "abc123",
                SessionId: "session-1",
                Source: "web",
                CountryTag: "cd"),
            CancellationToken.None);

        var dto = AssertOk(result);
        var row = await fixture.Db.SearchAnalyticsEvents
            .AsNoTracking()
            .SingleAsync(x => x.Id == dto.EventId);

        Assert.Equal(SearchAnalyticsEvents.Query, row.EventType);
        Assert.Equal("Apple Juice", row.Query);
        Assert.Equal("apple juice", row.NormalizedQuery);
        Assert.Equal(0, row.ResultsCount);
        Assert.Equal(145, row.DurationMs);
        Assert.Equal("Web", row.Source);
        Assert.Equal("CD", row.CountryTag);
        Assert.Equal(fixture.UserId, row.UserId);
    }

    [Fact]
    public async Task Track_GenericClickEvent_PersistsClick()
    {
        await using var fixture = await AnalyticsFixture.CreateAsync();
        var controller = fixture.CreateTrackingController();
        var targetId = Guid.NewGuid();

        var result = await controller.Track(
            new SearchAnalyticsController.TrackSearchEventRequest(
                EventName: "search_result_clicked",
                Query: "café machine",
                TargetType: SearchAnalyticsEvents.TargetProduct,
                TargetId: targetId,
                Position: 2,
                ResultsCount: null,
                DurationMs: null,
                Page: 1,
                Sort: "relevance",
                FiltersHash: "f1",
                SessionId: "session-track",
                Source: "mobile",
                CountryTag: "cd"),
            CancellationToken.None);

        var dto = AssertOk(result);
        var row = await fixture.Db.SearchAnalyticsEvents.AsNoTracking()
            .SingleAsync(x => x.Id == dto.EventId);

        Assert.Equal(SearchAnalyticsEvents.Click, row.EventType);
        Assert.Equal("cafe machine", row.NormalizedQuery);
        Assert.Equal(SearchAnalyticsEvents.TargetProduct, row.TargetType);
        Assert.Equal(targetId, row.TargetId);
    }

    [Fact]
    public async Task TrackClick_WithoutTargetId_ReturnsBadRequest()
    {
        await using var fixture = await AnalyticsFixture.CreateAsync();
        var controller = fixture.CreateTrackingController();

        var result = await controller.TrackClick(
            new SearchAnalyticsController.TrackSearchClickRequest(
                Query: "apple juice",
                TargetType: SearchAnalyticsEvents.TargetProduct,
                TargetId: null,
                Position: 1,
                Page: 1,
                Sort: null,
                FiltersHash: null,
                SessionId: "session-1",
                Source: "web",
                CountryTag: "CD"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AdminSearchAnalytics_ReturnsExpectedAggregates()
    {
        await using var fixture = await AnalyticsFixture.CreateAsync();
        var tracking = fixture.CreateTrackingController();

        // Two searches for "apple", one has no result.
        await tracking.TrackQuery(
            new SearchAnalyticsController.TrackSearchQueryRequest(
                Query: "apple",
                ResultsCount: 8,
                DurationMs: 120,
                Page: 1,
                Sort: "relevance",
                FiltersHash: null,
                SessionId: "s1",
                Source: "web",
                CountryTag: "CD"),
            CancellationToken.None);

        await tracking.TrackQuery(
            new SearchAnalyticsController.TrackSearchQueryRequest(
                Query: "apple",
                ResultsCount: 0,
                DurationMs: 150,
                Page: 1,
                Sort: "relevance",
                FiltersHash: null,
                SessionId: "s1",
                Source: "web",
                CountryTag: "CD"),
            CancellationToken.None);

        await tracking.TrackClick(
            new SearchAnalyticsController.TrackSearchClickRequest(
                Query: "apple",
                TargetType: SearchAnalyticsEvents.TargetProduct,
                TargetId: Guid.NewGuid(),
                Position: 1,
                Page: 1,
                Sort: "relevance",
                FiltersHash: null,
                SessionId: "s1",
                Source: "web",
                CountryTag: "CD"),
            CancellationToken.None);

        await tracking.TrackQuery(
            new SearchAnalyticsController.TrackSearchQueryRequest(
                Query: "banana",
                ResultsCount: 0,
                DurationMs: 90,
                Page: 1,
                Sort: "relevance",
                FiltersHash: null,
                SessionId: "s2",
                Source: "mobile",
                CountryTag: "CD"),
            CancellationToken.None);

        var admin = fixture.CreateAdminController();

        var kpiResult = await admin.GetKpis(null, null, null, CancellationToken.None);
        var kpis = AssertOk(kpiResult);
        Assert.Equal(3, kpis.Searches);
        Assert.Equal(1, kpis.Clicks);
        Assert.Equal(33.33m, kpis.ClickThroughRate);
        Assert.Equal(2, kpis.NoResultSearches);
        Assert.Equal(66.67m, kpis.NoResultRate);
        Assert.Equal(2, kpis.UniqueQueries);
        Assert.Equal(2, kpis.UniqueSessions);

        var topResult = await admin.GetTopQueries(null, null, null, 10, CancellationToken.None);
        var topRows = AssertOk(topResult);
        Assert.True(topRows.Count >= 2);
        Assert.Equal("apple", topRows[0].Query);
        Assert.Equal(2, topRows[0].Searches);
        Assert.Equal(1, topRows[0].Clicks);
        Assert.Equal(50m, topRows[0].ClickThroughRate);

        var noResult = await admin.GetNoResultQueries(null, null, null, 10, CancellationToken.None);
        var noResultRows = AssertOk(noResult);
        Assert.True(noResultRows.Count >= 2);
        Assert.Equal("apple", noResultRows[0].Query);
        Assert.Equal(1, noResultRows[0].Count);
        Assert.Equal("banana", noResultRows[1].Query);
        Assert.Equal(1, noResultRows[1].Count);
    }

    private static SearchAnalyticsController.SearchAnalyticsTrackedDto AssertOk(
        ActionResult<SearchAnalyticsController.SearchAnalyticsTrackedDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<SearchAnalyticsController.SearchAnalyticsTrackedDto>(ok.Value);
    }

    private static SearchAnalyticsCalculator.SearchAnalyticsKpis AssertOk(
        ActionResult<SearchAnalyticsCalculator.SearchAnalyticsKpis> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<SearchAnalyticsCalculator.SearchAnalyticsKpis>(ok.Value);
    }

    private static IReadOnlyList<SearchAnalyticsCalculator.TopSearchQueryRow> AssertOk(
        ActionResult<IReadOnlyList<SearchAnalyticsCalculator.TopSearchQueryRow>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IReadOnlyList<SearchAnalyticsCalculator.TopSearchQueryRow>>(ok.Value);
    }

    private static IReadOnlyList<SearchAnalyticsCalculator.NoResultSearchQueryRow> AssertOk(
        ActionResult<IReadOnlyList<SearchAnalyticsCalculator.NoResultSearchQueryRow>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IReadOnlyList<SearchAnalyticsCalculator.NoResultSearchQueryRow>>(ok.Value);
    }

    private sealed class AnalyticsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AnalyticsFixture(SqliteConnection connection, DoorMarketDbContext db, Guid userId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid UserId { get; }

        public SearchAnalyticsController CreateTrackingController()
            => new(Db, new TestCurrentUserService(UserId));

        public AdminSearchAnalyticsController CreateAdminController()
            => new(new SearchAnalyticsCalculator(Db));

        public static async Task<AnalyticsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            return new AnalyticsFixture(connection, db, Guid.NewGuid());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Email => "user@test.local";
        public string? Role => "Client";
        public bool IsAuthenticated => true;
    }
}
