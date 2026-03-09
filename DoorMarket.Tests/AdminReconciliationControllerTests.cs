using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminReconciliationControllerTests
{
    [Fact]
    public async Task CreatePayout_WithIdempotencyKey_ReturnsExistingPayout()
    {
        await using var fixture = await PayoutFixture.CreateAsync();
        var controller = fixture.CreateController();

        var request = new AdminReconciliationController.CreateShopPayoutRequest(
            ShopId: fixture.ShopId,
            Currency: "USD",
            PeriodStartUtc: fixture.PeriodStartUtc,
            PeriodEndUtc: fixture.PeriodEndUtc,
            AmountPaid: 10m,
            Reference: "REF-1",
            IdempotencyKey: "idem-001");

        var first = await controller.CreatePayout(request, CancellationToken.None);
        var second = await controller.CreatePayout(request, CancellationToken.None);

        var dto1 = AssertOk(first);
        var dto2 = AssertOk(second);

        Assert.Equal(dto1.Id, dto2.Id);
        Assert.Equal("Draft", dto1.Status);
        Assert.Equal("USD", dto1.Currency);
    }

    [Fact]
    public async Task PayoutWorkflow_Approve_Pay_Reverse_Works()
    {
        await using var fixture = await PayoutFixture.CreateAsync();
        var controller = fixture.CreateController();

        var create = await controller.CreatePayout(
            new AdminReconciliationController.CreateShopPayoutRequest(
                ShopId: fixture.ShopId,
                Currency: "USD",
                PeriodStartUtc: fixture.PeriodStartUtc,
                PeriodEndUtc: fixture.PeriodEndUtc,
                AmountPaid: 10m,
                Reference: null,
                IdempotencyKey: null),
            CancellationToken.None);
        var createdDto = AssertOk(create);

        var approve = await controller.ApprovePayout(
            createdDto.Id,
            new AdminReconciliationController.PayoutTransitionRequest("approve", null),
            CancellationToken.None);
        Assert.Equal("Approved", AssertOk(approve).Status);

        var paid = await controller.MarkPayoutAsPaid(
            createdDto.Id,
            new AdminReconciliationController.MarkPayoutPaidRequest(9m, DateTime.UtcNow, "wire-001", "paid", null),
            CancellationToken.None);
        var paidDto = AssertOk(paid);
        Assert.Equal("Paid", paidDto.Status);
        Assert.Equal(9m, paidDto.AmountPaid);

        var reversed = await controller.ReversePayout(
            createdDto.Id,
            new AdminReconciliationController.ReversePayoutRequest("bank correction", null),
            CancellationToken.None);
        Assert.Equal("Reversed", AssertOk(reversed).Status);
    }

    [Fact]
    public async Task MarkPaid_FromDraft_ReturnsConflict()
    {
        await using var fixture = await PayoutFixture.CreateAsync();
        var controller = fixture.CreateController();

        var create = await controller.CreatePayout(
            new AdminReconciliationController.CreateShopPayoutRequest(
                ShopId: fixture.ShopId,
                Currency: "USD",
                PeriodStartUtc: fixture.PeriodStartUtc,
                PeriodEndUtc: fixture.PeriodEndUtc,
                AmountPaid: 10m,
                Reference: null,
                IdempotencyKey: null),
            CancellationToken.None);
        var createdDto = AssertOk(create);

        var result = await controller.MarkPayoutAsPaid(
            createdDto.Id,
            new AdminReconciliationController.MarkPayoutPaidRequest(null, DateTime.UtcNow, null, null, null),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreatePayout_WithOverlappingPeriod_ReturnsConflict()
    {
        await using var fixture = await PayoutFixture.CreateAsync();
        var controller = fixture.CreateController();

        var first = await controller.CreatePayout(
            new AdminReconciliationController.CreateShopPayoutRequest(
                ShopId: fixture.ShopId,
                Currency: "USD",
                PeriodStartUtc: fixture.PeriodStartUtc,
                PeriodEndUtc: fixture.PeriodEndUtc,
                AmountPaid: 5m,
                Reference: null,
                IdempotencyKey: null),
            CancellationToken.None);
        AssertOk(first);

        var overlap = await controller.CreatePayout(
            new AdminReconciliationController.CreateShopPayoutRequest(
                ShopId: fixture.ShopId,
                Currency: "USD",
                PeriodStartUtc: fixture.PeriodStartUtc.AddHours(-6),
                PeriodEndUtc: fixture.PeriodEndUtc.AddHours(-6),
                AmountPaid: 5m,
                Reference: null,
                IdempotencyKey: null),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(overlap.Result);
    }

    [Fact]
    public async Task MarkPaid_WithDateBeforePeriodStart_ReturnsBadRequest()
    {
        await using var fixture = await PayoutFixture.CreateAsync();
        var controller = fixture.CreateController();

        var created = await controller.CreatePayout(
            new AdminReconciliationController.CreateShopPayoutRequest(
                ShopId: fixture.ShopId,
                Currency: "USD",
                PeriodStartUtc: fixture.PeriodStartUtc,
                PeriodEndUtc: fixture.PeriodEndUtc,
                AmountPaid: 8m,
                Reference: null,
                IdempotencyKey: null),
            CancellationToken.None);
        var createdDto = AssertOk(created);

        var approved = await controller.ApprovePayout(
            createdDto.Id,
            new AdminReconciliationController.PayoutTransitionRequest("ok", null),
            CancellationToken.None);
        Assert.Equal("Approved", AssertOk(approved).Status);

        var paid = await controller.MarkPayoutAsPaid(
            createdDto.Id,
            new AdminReconciliationController.MarkPayoutPaidRequest(8m, fixture.PeriodStartUtc.AddDays(-1), null, null, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(paid.Result);
    }

    [Fact]
    public async Task AssistantInsights_DetectsStaleDraftAndPartialPaid()
    {
        await using var fixture = await PayoutFixture.CreateAsync();

        fixture.Db.ShopPayouts.Add(new ShopPayout
        {
            ShopId = fixture.ShopId,
            Currency = "USD",
            Status = "Draft",
            PeriodStartUtc = DateTime.UtcNow.AddDays(-20),
            PeriodEndUtc = DateTime.UtcNow.AddDays(-19),
            GrossSalesItems = 100m,
            PlatformFee = 10m,
            NetToPay = 90m,
            AmountPaid = 0m,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-12)
        });

        fixture.Db.ShopPayouts.Add(new ShopPayout
        {
            ShopId = fixture.ShopId,
            Currency = "USD",
            Status = "Paid",
            PeriodStartUtc = DateTime.UtcNow.AddDays(-10),
            PeriodEndUtc = DateTime.UtcNow.AddDays(-9),
            GrossSalesItems = 120m,
            PlatformFee = 20m,
            NetToPay = 100m,
            AmountPaid = 80m,
            Reference = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-7),
            PaidOutAtUtc = DateTime.UtcNow.AddDays(-6)
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.GetAssistantInsights(
            from: DateTime.UtcNow.AddDays(-60),
            to: DateTime.UtcNow,
            shopId: fixture.ShopId,
            draftSlaDays: 7,
            approvedSlaDays: 3,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminReconciliationController.ReconciliationAssistantDto>(ok.Value);

        Assert.Contains(payload.Insights, x => x.Code == "STALE_DRAFT_PAYOUTS");
        Assert.Contains(payload.Insights, x => x.Code == "PARTIAL_PAID_DELTA");
        Assert.Contains(payload.Insights, x => x.Code == "PAID_WITHOUT_REFERENCE");
        Assert.Equal(1, payload.Summary.StaleDraftCount);
        Assert.Equal(1, payload.Summary.PartialPaidCount);
    }

    private static AdminReconciliationController.ShopPayoutDto AssertOk(ActionResult<AdminReconciliationController.ShopPayoutDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AdminReconciliationController.ShopPayoutDto>(ok.Value);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Email => "admin@test.local";
        public string? Role => "Admin";
        public bool IsAuthenticated => true;
    }

    private sealed class PayoutFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PayoutFixture(SqliteConnection connection, DoorMarketDbContext db, Guid shopId, Guid adminUserId, DateTime periodStartUtc, DateTime periodEndUtc)
        {
            _connection = connection;
            Db = db;
            ShopId = shopId;
            AdminUserId = adminUserId;
            PeriodStartUtc = periodStartUtc;
            PeriodEndUtc = periodEndUtc;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ShopId { get; }
        public Guid AdminUserId { get; }
        public DateTime PeriodStartUtc { get; }
        public DateTime PeriodEndUtc { get; }

        public AdminReconciliationController CreateController()
            => new(Db, new FinanceCalculator(Db), new TestCurrentUserService(AdminUserId), new FakeReconciliationCronService());

        public static async Task<PayoutFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var adminUser = new User
            {
                Email = "admin@test.local",
                PasswordHash = "hash",
                Role = UserRole.Admin
            };

            var shopOwner = new User
            {
                Email = "owner@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Category 1",
                Slug = "category-1"
            };

            var shop = new Shop
            {
                OwnerUser = shopOwner,
                Name = "Shop 1",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Product 1",
                Price = 20m,
                Currency = "USD",
                StockQty = 50,
                IsActive = true
            };

            var paidAt = DateTime.UtcNow.AddDays(-2);
            var order = new Order
            {
                User = adminUser,
                Status = "Paid",
                PaymentStatus = "Paid",
                FulfillmentStatus = "PaidPending",
                PaymentProvider = "PayPal",
                Currency = "USD",
                Subtotal = 20m,
                TotalItemsAmount = 20m,
                PlatformFeeTotal = 2m,
                DeliveryFee = 0m,
                Discount = 0m,
                TotalAmount = 20m,
                DeliveryName = "Client",
                DeliveryPhone = "+2430000000",
                DeliveryLine1 = "Address",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                PaidAtUtc = paidAt
            };

            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                Qty = 1,
                UnitPrice = 20m,
                UnitPriceAtPurchase = 20m,
                PlatformFeeAtPurchase = 2m,
                LineTotal = 20m
            };

            db.AddRange(adminUser, shopOwner, category, shop, product, order, orderItem);
            await db.SaveChangesAsync();

            var periodStart = paidAt.AddDays(-1);
            var periodEnd = paidAt.AddDays(1);

            return new PayoutFixture(connection, db, shop.Id, adminUser.Id, periodStart, periodEnd);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeReconciliationCronService : IReconciliationCronService
    {
        public Task<ReconciliationCronRunResultDto> RunOnceAsync(string triggerSource, Guid? triggeredByUserId, CancellationToken ct = default)
            => Task.FromResult(new ReconciliationCronRunResultDto(
                RunId: Guid.NewGuid(),
                AcquiredLock: true,
                Success: true,
                WasSkipped: false,
                SkipReason: null,
                TriggerSource: triggerSource,
                TriggeredByUserId: triggeredByUserId,
                StartedAtUtc: DateTime.UtcNow,
                EndedAtUtc: DateTime.UtcNow,
                DurationMs: 0,
                WindowFromUtc: DateTime.UtcNow.AddDays(-1),
                WindowToUtc: DateTime.UtcNow,
                DraftSlaDays: 7,
                ApprovedSlaDays: 3,
                CandidatePayouts: 0,
                StaleDraftCount: 0,
                StaleApprovedCount: 0,
                PartialPaidCount: 0,
                PaidWithoutReferenceCount: 0,
                OverlapPairCount: 0,
                ReversedCount: 0,
                ReversalRatePercent: 0m,
                PartialGapTotal: 0m,
                InsightsCount: 0,
                Error: null));

        public Task<ReconciliationCronRunsPageDto> GetRunsAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new ReconciliationCronRunsPageDto(
                Page: page,
                PageSize: pageSize,
                Total: 0,
                Items: new List<ReconciliationCronRunResultDto>()));

        public Task<ReconciliationCronSummaryDto> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
            => Task.FromResult(new ReconciliationCronSummaryDto(
                FromUtc: fromUtc ?? DateTime.UtcNow.AddDays(-30),
                ToUtc: toUtc ?? DateTime.UtcNow,
                TotalRuns: 0,
                SuccessRuns: 0,
                FailedRuns: 0,
                SkippedRuns: 0,
                ActiveRuns: 0,
                RunDurationP50Ms: null,
                RunDurationP95Ms: null,
                LastRunAtUtc: null,
                LastSuccessAtUtc: null,
                LastFailureAtUtc: null,
                LastSkippedAtUtc: null,
                LastActiveStartedAtUtc: null,
                TotalInsights: 0,
                TotalCandidatePayouts: 0,
                AvgCandidatePayoutsPerRun: 0m,
                AvgInsightsPerRun: 0m,
                TotalOverlapPairCount: 0,
                TotalPartialGap: 0m));
    }
}
