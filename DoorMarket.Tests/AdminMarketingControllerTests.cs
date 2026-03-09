using DoorMarket.Api.Controllers;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminMarketingControllerTests
{
    [Fact]
    public async Task GetBanners_WithStatusLive_ReturnsOnlyLiveRows()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetBanners(
            q: null,
            active: null,
            status: "live",
            page: 1,
            pageSize: 50,
            ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.Equal(2, payload.Items.Count);
        Assert.Contains(payload.Items, x => x.Title == "Live banner");
        Assert.Contains(payload.Items, x => x.Title == "Live banner low ctr");
    }

    [Fact]
    public async Task Create_WithInvalidPeriod_ReturnsBadRequest()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var now = DateTime.UtcNow;
        var response = await controller.Create(
            new DoorMarket.Application.DTOs.Marketing.CreateMarketingBannerRequest(
                Title: "Invalid period",
                Subtitle: null,
                ImageUrl: null,
                TargetUrl: null,
                IsActive: true,
                StartAtUtc: now.AddDays(2),
                EndAtUtc: now,
                Language: null,
                City: null,
                Zone: null,
                CategoryId: null,
                ShopId: null,
                SortOrder: 0),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task GetSummary_ComputesCtrPercent()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetSummary(
            q: null,
            active: null,
            status: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingSummaryDto>(ok.Value);

        Assert.Equal(5, payload.Total);
        Assert.Equal(2, payload.Live);
        Assert.Equal(1, payload.Planned);
        Assert.Equal(1, payload.Expired);
        Assert.Equal(1, payload.Inactive);
        Assert.Equal(280, payload.Impressions);
        Assert.Equal(20, payload.Clicks);
        Assert.Equal(7.14m, payload.CtrPercent);
    }

    [Fact]
    public async Task RunAutomation_DisablesExpiredAndReprioritizesLiveBanners()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.RunAutomation(
            new AdminMarketingController.RunMarketingAutomationRequest(
                DisableExpired: true,
                AutoPrioritizeLiveByCtr: true,
                MinImpressionsForCtr: 50),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingAutomationResultDto>(ok.Value);
        Assert.Equal(1, payload.DisabledExpired);
        Assert.True(payload.ReprioritizedLive >= 1);

        var expired = await fixture.Db.MarketingBanners.AsNoTracking().SingleAsync(x => x.Title == "Expired banner");
        Assert.False(expired.IsActive);

        var liveA = await fixture.Db.MarketingBanners.AsNoTracking().SingleAsync(x => x.Title == "Live banner");
        var liveB = await fixture.Db.MarketingBanners.AsNoTracking().SingleAsync(x => x.Title == "Live banner low ctr");
        Assert.True(liveA.SortOrder < liveB.SortOrder);
    }

    [Fact]
    public async Task GetCampaignSummary_ReturnsCampaignAndRoiAggregates()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetCampaignSummary(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingCampaignSummaryDto>(ok.Value);

        Assert.True(payload.TotalCampaigns >= 4);
        Assert.True(payload.ActiveCampaigns >= 2);
        Assert.True(payload.RunsCount >= 1);
        Assert.True(payload.RevenueAttributed >= 200m);
        Assert.True(payload.DiscountCost >= 20m);
    }

    [Fact]
    public async Task RunCampaignAutomation_CreatesRunsAndCompletesExpiredCampaigns()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var beforeRunCount = await fixture.Db.MarketingCampaignRuns.AsNoTracking().CountAsync();

        var result = await controller.RunCampaignAutomation(
            new AdminMarketingController.RunCampaignBatchAutomationRequest(
                DryRun: false,
                IncludeDraft: false,
                AutoCompleteExpired: true,
                MaxCampaigns: 10,
                AttributionWindowDays: 30,
                RunType: "Scheduled"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingCampaignBatchAutomationResultDto>(ok.Value);

        Assert.True(payload.ProcessedCampaigns >= 1);
        Assert.True(payload.CreatedRuns >= 1);
        Assert.True(payload.CompletedExpiredCampaigns >= 1);

        var afterRunCount = await fixture.Db.MarketingCampaignRuns.AsNoTracking().CountAsync();
        Assert.True(afterRunCount > beforeRunCount);

        var expiredCampaign = await fixture.Db.MarketingCampaigns.AsNoTracking()
            .SingleAsync(x => x.Name == "Campaign expired active");
        Assert.Equal("Completed", expiredCampaign.Status);
    }

    [Fact]
    public async Task GetRfmInsights_ReturnsTagsAndUsers()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetRfmInsights(windowDays: 180, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingRfmInsightsDto>(ok.Value);

        Assert.True(payload.TotalActiveClients >= 2);
        Assert.True(payload.ClientsWithPaidOrder >= 1);
        Assert.NotEmpty(payload.Tags);
        Assert.NotEmpty(payload.Users);
    }

    [Fact]
    public async Task BootstrapRfmSegments_CreatesSystemSegments()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var before = await fixture.Db.MarketingSegments.AsNoTracking().CountAsync(x => x.IsSystem && x.Name.StartsWith("RFM - "));

        var result = await controller.BootstrapRfmSegments(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingRfmBootstrapResultDto>(ok.Value);
        Assert.Equal(6, payload.TotalTemplates);

        var after = await fixture.Db.MarketingSegments.AsNoTracking().CountAsync(x => x.IsSystem && x.Name.StartsWith("RFM - "));
        Assert.True(after >= before);
        Assert.True(after >= 6);
    }

    [Fact]
    public async Task RunScenarioAutomation_CreatesScenarioCampaignRuns()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var beforeRuns = await fixture.Db.MarketingCampaignRuns.AsNoTracking().CountAsync();

        var result = await controller.RunScenarioAutomation(
            new AdminMarketingController.RunMarketingScenarioAutomationRequest(
                DryRun: false,
                IncludeWelcome: true,
                IncludeWinback: true,
                IncludeChurnRisk: true,
                MaxScenarios: 10,
                AttributionWindowDays: 30),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingScenarioAutomationResultDto>(ok.Value);

        Assert.True(payload.PlannedScenarios >= 3);
        Assert.True(payload.EnsuredCampaigns >= 3);

        var afterRuns = await fixture.Db.MarketingCampaignRuns.AsNoTracking().CountAsync();
        Assert.True(afterRuns >= beforeRuns);
    }

    [Fact]
    public async Task RunScenarioAutomation_AppliesAntiSpamPerScenarioStepAndUser()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.RunScenarioAutomation(
            new AdminMarketingController.RunMarketingScenarioAutomationRequest(
                DryRun: false,
                IncludeWelcome: true,
                IncludeWinback: false,
                IncludeChurnRisk: false,
                MaxScenarios: 1,
                AttributionWindowDays: 30,
                AntiSpamStep1Hours: 1,
                AntiSpamStep2Hours: 1,
                AntiSpamStep3Hours: 1,
                ScenarioWindowDays: 7,
                ScenarioMaxTouchesPerUser: 1),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingScenarioAutomationResultDto>(ok.Value);

        Assert.Equal(1, payload.PlannedScenarios);
        Assert.True(payload.ExecutedRuns >= 1);
        Assert.True(payload.AntiSpamSkippedUsers > 0);
        Assert.Contains(payload.StepsExecuted, x => x.StepCode == "S2" && x.AntiSpamSkipped > 0);

        var scenarioSentLogs = await fixture.Db.TransactionalNotificationLogs.AsNoTracking()
            .CountAsync(x => x.NotificationType.StartsWith("marketing_scenario:welcome:") && x.Status == "Sent");
        Assert.True(scenarioSentLogs > 0);
    }

    [Fact]
    public async Task GetCampaignRoi_WithSegmentAndChannelFilters_ReturnsScopedRows()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var segmentId = await fixture.Db.MarketingSegments.AsNoTracking()
            .Where(x => x.Name == "Kinshasa clients")
            .Select(x => x.Id)
            .FirstAsync();

        var result = await controller.GetCampaignRoi(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: "active",
            segmentId: segmentId,
            channel: "email",
            minSent: 1,
            minRoi: null,
            page: 1,
            pageSize: 100,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DoorMarket.Application.Common.PagedResult<AdminMarketingController.MarketingCampaignRoiDto>>(ok.Value);
        Assert.NotEmpty(payload.Items);
        Assert.All(payload.Items, row =>
        {
            Assert.Equal(segmentId, row.SegmentId);
            Assert.Contains("Email", row.Channels);
            Assert.True(row.SentCount >= 1);
        });
    }

    [Fact]
    public async Task GetCampaignCohorts_ReturnsSegmentAggregation()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetCampaignCohorts(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<List<AdminMarketingController.MarketingCampaignCohortDto>>(ok.Value);
        Assert.NotEmpty(payload);
        Assert.Contains(payload, x => x.SegmentName == "Kinshasa clients");
    }

    [Fact]
    public async Task ExportCampaignKpiCsv_ReturnsCsvWithSummaryAndCohorts()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.ExportCampaignKpiCsv(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Summary,TotalCampaigns", csv);
        Assert.Contains("Section,Segment,CampaignCount", csv);
        Assert.Contains("Campaign,", csv);
    }

    [Fact]
    public async Task ExportCampaignDashboardCsv_ReturnsCsvWithTimeseriesAndInsights()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.ExportCampaignDashboardCsv(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            granularity: "week",
            ct: CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Summary,TotalCampaigns", csv);
        Assert.Contains("Summary,Granularity,week", csv);
        Assert.Contains("Section,PeriodStartUtc,RunsCount,SentCount,RevenueAttributed,DiscountCost,RoiPercent", csv);
        Assert.Contains("Insights,Top,", csv);
        Assert.Contains("Insights,Bottom,", csv);
    }

    [Fact]
    public async Task PreviewSegment_ReturnsAudienceAndSample()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.PreviewSegment(
            new AdminMarketingController.PreviewMarketingSegmentRequest(
                CriteriaJson: "{\"city\":\"Kinshasa\"}",
                SampleSize: 10),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingSegmentPreviewDto>(ok.Value);
        Assert.True(payload.AudienceCount >= 1);
        Assert.NotEmpty(payload.SampleUsers);
        Assert.Contains(payload.SampleUsers, x => string.Equals(x.Email, "clientA@doormarket.test", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetCampaignTimeSeries_ReturnsOrderedPoints()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetCampaignTimeSeries(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            granularity: "day",
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingCampaignTimeSeriesDto>(ok.Value);
        Assert.Equal("day", payload.Granularity);
        Assert.NotEmpty(payload.Points);
        Assert.True(payload.Points[0].PeriodStartUtc <= payload.Points[^1].PeriodStartUtc);
    }

    [Fact]
    public async Task GetCampaignExperiments_ReturnsRunTypeBuckets()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.GetCampaignExperiments(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<AdminMarketingController.MarketingCampaignExperimentDto>>(ok.Value);
        Assert.NotEmpty(payload);
        Assert.Contains(payload, x => x.Experiment is "Manual" or "Scheduled");
    }

    [Fact]
    public async Task ExportCampaignExperimentsCsv_ReturnsCsvWithExperimentRows()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        _ = await controller.RunCampaignAutomation(
            new AdminMarketingController.RunCampaignBatchAutomationRequest(
                DryRun: false,
                IncludeDraft: true,
                AutoCompleteExpired: false,
                MaxCampaigns: 5,
                AttributionWindowDays: 30,
                RunType: "Scheduled"),
            CancellationToken.None);

        var result = await controller.ExportCampaignExperimentsCsv(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Experiment,CampaignsCount,RunsCount", csv);
        Assert.Contains("Scheduled", csv);
    }

    [Fact]
    public async Task GetCampaignExperimentInsights_ReturnsUpliftAndRecommendations()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        _ = await controller.RunCampaignAutomation(
            new AdminMarketingController.RunCampaignBatchAutomationRequest(
                DryRun: false,
                IncludeDraft: true,
                AutoCompleteExpired: false,
                MaxCampaigns: 5,
                AttributionWindowDays: 30,
                RunType: "Scheduled"),
            CancellationToken.None);

        var result = await controller.GetCampaignExperimentInsights(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingCampaignExperimentInsightsDto>(ok.Value);
        Assert.True(payload.HasPair);
        Assert.NotNull(payload.Baseline);
        Assert.NotNull(payload.Variant);
        Assert.NotEmpty(payload.Recommendations);
    }

    [Fact]
    public async Task ExportCampaignExperimentInsightsCsv_ReturnsCsvWithRecommendations()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        _ = await controller.RunCampaignAutomation(
            new AdminMarketingController.RunCampaignBatchAutomationRequest(
                DryRun: false,
                IncludeDraft: true,
                AutoCompleteExpired: false,
                MaxCampaigns: 5,
                AttributionWindowDays: 30,
                RunType: "Scheduled"),
            CancellationToken.None);

        var result = await controller.ExportCampaignExperimentInsightsCsv(
            from: DateTime.UtcNow.AddDays(-30),
            to: DateTime.UtcNow.AddDays(1),
            q: null,
            status: null,
            segmentId: null,
            channel: null,
            minSent: null,
            minRoi: null,
            ct: CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Summary,HasPair", csv);
        Assert.Contains("Section,Priority,Action,Rationale,ExecutionHint", csv);
        Assert.Contains("Recommendation,", csv);
    }

    [Fact]
    public async Task RunScenarioAutomation_WithChannelSplit_CreatesChannelCampaignRuns()
    {
        await using var fixture = await MarketingFixture.CreateAsync();
        var controller = new AdminMarketingController(fixture.Db);

        var result = await controller.RunScenarioAutomation(
            new AdminMarketingController.RunMarketingScenarioAutomationRequest(
                DryRun: false,
                IncludeWelcome: true,
                IncludeWinback: false,
                IncludeChurnRisk: false,
                MaxScenarios: 1,
                AttributionWindowDays: 30,
                AntiSpamStep1Hours: 1,
                AntiSpamStep2Hours: 1,
                AntiSpamStep3Hours: 1,
                ScenarioWindowDays: 7,
                ScenarioMaxTouchesPerUser: 6,
                SplitChannelsToCampaigns: true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminMarketingController.MarketingScenarioAutomationResultDto>(ok.Value);
        Assert.True(payload.ExecutedRuns >= 2);
        Assert.Contains(payload.StepsExecuted, x => x.CampaignName.Contains("Email"));
        Assert.Contains(payload.StepsExecuted, x => x.CampaignName.Contains("InApp"));
    }

    private static DoorMarket.Application.Common.PagedResult<DoorMarket.Application.DTOs.Marketing.MarketingBannerDto> AssertOk(
        ActionResult<DoorMarket.Application.Common.PagedResult<DoorMarket.Application.DTOs.Marketing.MarketingBannerDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<DoorMarket.Application.Common.PagedResult<DoorMarket.Application.DTOs.Marketing.MarketingBannerDto>>(ok.Value);
    }

    private sealed class MarketingFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private MarketingFixture(SqliteConnection connection, DoorMarketDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public DoorMarketDbContext Db { get; }

        public static async Task<MarketingFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;
            db.MarketingBanners.AddRange(
                new MarketingBanner
                {
                    Title = "Live banner",
                    IsActive = true,
                    SortOrder = 5,
                    StartAtUtc = now.AddDays(-1),
                    EndAtUtc = now.AddDays(2),
                    Impressions = 100,
                    Clicks = 10
                },
                new MarketingBanner
                {
                    Title = "Live banner low ctr",
                    IsActive = true,
                    SortOrder = 1,
                    StartAtUtc = now.AddDays(-2),
                    EndAtUtc = now.AddDays(4),
                    Impressions = 100,
                    Clicks = 2
                },
                new MarketingBanner
                {
                    Title = "Planned banner",
                    IsActive = true,
                    StartAtUtc = now.AddDays(1),
                    EndAtUtc = now.AddDays(3),
                    Impressions = 50,
                    Clicks = 5
                },
                new MarketingBanner
                {
                    Title = "Expired banner",
                    IsActive = true,
                    StartAtUtc = now.AddDays(-5),
                    EndAtUtc = now.AddDays(-1),
                    Impressions = 20,
                    Clicks = 2
                },
                new MarketingBanner
                {
                    Title = "Inactive banner",
                    IsActive = false,
                    Impressions = 10,
                    Clicks = 1
                });

            var segmentKinshasa = new MarketingSegment
            {
                Name = "Kinshasa clients",
                CriteriaJson = "{\"city\":\"Kinshasa\"}",
                IsActive = true,
                IsSystem = false
            };

            var couponKin10 = new Coupon
            {
                Code = "KIN10",
                Name = "Kin 10",
                IsActive = true,
                DiscountType = "Percent",
                DiscountValue = 10m,
                ScopeType = "Global"
            };

            db.MarketingSegments.Add(segmentKinshasa);
            db.Coupons.Add(couponKin10);

            var userA = new User
            {
                Email = "clientA@doormarket.test",
                PasswordHash = "hash",
                Role = DoorMarket.Domain.Enums.UserRole.Client,
                IsActive = true
            };
            var userB = new User
            {
                Email = "clientB@doormarket.test",
                PasswordHash = "hash",
                Role = DoorMarket.Domain.Enums.UserRole.Client,
                IsActive = true
            };

            db.Users.AddRange(userA, userB);
            await db.SaveChangesAsync();

            db.Orders.AddRange(
                CreatePaidOrder(userA.Id, now.AddDays(-3), "Kinshasa", "CD", promoCode: "KIN10", totalAmount: 200m, discount: 20m),
                CreatePaidOrder(userB.Id, now.AddDays(-2), "Lubumbashi", "CD", promoCode: null, totalAmount: 80m, discount: 0m));

            var campaignA = new MarketingCampaign
            {
                Name = "Campaign active kin",
                Status = "Active",
                SegmentId = segmentKinshasa.Id,
                CouponId = couponKin10.Id,
                ChannelEmail = true,
                StartAtUtc = now.AddDays(-7),
                EndAtUtc = now.AddDays(7),
                LastRunAtUtc = now.AddDays(-10)
            };
            var campaignB = new MarketingCampaign
            {
                Name = "Campaign active broad",
                Status = "Active",
                ChannelPush = true,
                StartAtUtc = now.AddDays(-2),
                EndAtUtc = now.AddDays(3)
            };
            var campaignCompleted = new MarketingCampaign
            {
                Name = "Campaign completed",
                Status = "Completed",
                ChannelInApp = true,
                StartAtUtc = now.AddDays(-20),
                EndAtUtc = now.AddDays(-10)
            };
            var campaignExpiredActive = new MarketingCampaign
            {
                Name = "Campaign expired active",
                Status = "Active",
                ChannelEmail = true,
                StartAtUtc = now.AddDays(-10),
                EndAtUtc = now.AddDays(-1)
            };

            db.MarketingCampaigns.AddRange(campaignA, campaignB, campaignCompleted, campaignExpiredActive);
            await db.SaveChangesAsync();

            db.MarketingCampaignRuns.Add(new MarketingCampaignRun
            {
                CampaignId = campaignA.Id,
                RunType = "Manual",
                Status = "Completed",
                TargetUsers = 120,
                SentCount = 100,
                FailedCount = 2,
                RevenueAttributed = 200m,
                DiscountCost = 20m,
                StartedAtUtc = now.AddDays(-1),
                CompletedAtUtc = now.AddDays(-1).AddMinutes(1),
                Notes = "seed run"
            });

            await db.SaveChangesAsync();
            return new MarketingFixture(connection, db);
        }

        private static Order CreatePaidOrder(
            Guid userId,
            DateTime createdAtUtc,
            string city,
            string country,
            string? promoCode,
            decimal totalAmount,
            decimal discount)
        {
            return new Order
            {
                UserId = userId,
                Status = "Completed",
                Subtotal = totalAmount + discount,
                DeliveryFee = 0m,
                Discount = discount,
                PromoCode = promoCode,
                DeliveryName = "Test User",
                DeliveryPhone = "+243000000000",
                DeliveryLine1 = "Test line",
                DeliveryCity = city,
                DeliveryCountry = country,
                PaymentProvider = "MobileMoney",
                PaymentStatus = "Paid",
                FulfillmentStatus = "Delivered",
                TotalItemsAmount = totalAmount + discount,
                PlatformFeeTotal = 0m,
                TotalAmount = totalAmount,
                Currency = "USD",
                CreatedAtUtc = createdAtUtc
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
