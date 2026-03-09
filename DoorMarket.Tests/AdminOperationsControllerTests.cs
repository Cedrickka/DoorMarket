using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminOperationsControllerTests
{
    [Fact]
    public async Task GetSnapshot_With24hWindow_ReturnsExpectedMetrics()
    {
        await using var fixture = await OperationsFixture.CreateAsync();
        var controller = new AdminOperationsController(fixture.Db);

        var result = await controller.GetSnapshot(hours: 24, staleCartHours: 24, ct: CancellationToken.None);

        var payload = AssertOk(result);
        Assert.Equal(2, payload.OrdersCreated);
        Assert.Equal(1, payload.OrdersPaid);
        Assert.Equal(1, payload.OrdersFailed);
        Assert.Equal(1, payload.CartUsersActive);
        Assert.Equal(1, payload.CheckoutUsers);
        Assert.Equal(100m, payload.CheckoutUserConversionRate);
        Assert.Equal(3, payload.CartsWithItems);
        Assert.Equal(2, payload.StaleCarts);
        Assert.Equal(2, payload.PayoutBacklogCount);
        Assert.Equal(100m, payload.PayoutBacklogAmount);
        Assert.Equal(1, payload.ActiveProductsOutOfStock);
        Assert.Equal(1, payload.ActiveProductsLowStock);
        Assert.Equal(1, payload.PendingShopApplications);
        Assert.Equal(1, payload.LiveBanners);
        Assert.Equal(4, payload.NotificationAttemptsInWindow);
        Assert.Equal(3, payload.NotificationFailedInWindow);
        Assert.Equal(3, payload.NotificationFailedUnresolved);
    }

    [Fact]
    public async Task GetSnapshot_WithTooLargeWindow_ClampsHoursAndStaleWindow()
    {
        await using var fixture = await OperationsFixture.CreateAsync();
        var controller = new AdminOperationsController(fixture.Db);

        var result = await controller.GetSnapshot(hours: 9999, staleCartHours: 9999, ct: CancellationToken.None);

        var payload = AssertOk(result);

        Assert.Equal(3, payload.OrdersCreated);
        Assert.Equal(2, payload.OrdersPaid);
        Assert.Equal(1, payload.OrdersFailed);
        Assert.Equal(1, payload.StaleCarts);
        Assert.Equal(4, payload.NotificationAttemptsInWindow);
        Assert.Equal(3, payload.NotificationFailedInWindow);
        Assert.Equal(3, payload.NotificationFailedUnresolved);

        var windowHours = (payload.ToUtc - payload.FromUtc).TotalHours;
        Assert.InRange(windowHours, 335.99, 336.01);
    }

    private static AdminOperationsController.OperationsSnapshotDto AssertOk(
        ActionResult<AdminOperationsController.OperationsSnapshotDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AdminOperationsController.OperationsSnapshotDto>(ok.Value);
    }

    private sealed class OperationsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private OperationsFixture(SqliteConnection connection, DoorMarketDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public DoorMarketDbContext Db { get; }

        public static async Task<OperationsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;

            var userA = new User
            {
                Email = "ops-user-a@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var userB = new User
            {
                Email = "ops-user-b@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var userC = new User
            {
                Email = "ops-user-c@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var owner = new User
            {
                Email = "ops-owner@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Ops Category",
                Slug = "ops-category"
            };

            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "Ops Shop",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var productOut = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Out product",
                Price = 10m,
                Currency = "USD",
                StockQty = 0,
                IsActive = true
            };

            var productLow = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Low product",
                Price = 20m,
                Currency = "USD",
                StockQty = 3,
                IsActive = true
            };

            var productHealthy = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Healthy product",
                Price = 30m,
                Currency = "USD",
                StockQty = 15,
                IsActive = true
            };

            var cartA = new Cart
            {
                User = userA
            };

            var cartB = new Cart
            {
                User = userB
            };

            var cartC = new Cart
            {
                User = userC
            };

            var cartItemA = new CartItem
            {
                Cart = cartA,
                Product = productHealthy,
                Qty = 1,
                UnitPrice = 30m,
                CreatedAtUtc = now.AddHours(-1)
            };

            var cartItemB = new CartItem
            {
                Cart = cartB,
                Product = productLow,
                Qty = 1,
                UnitPrice = 20m,
                CreatedAtUtc = now.AddHours(-48)
            };

            var cartItemC = new CartItem
            {
                Cart = cartC,
                Product = productHealthy,
                Qty = 1,
                UnitPrice = 30m,
                CreatedAtUtc = now.AddHours(-800)
            };

            var orderPaid = BuildOrder(userA, "Paid", now.AddHours(-2));
            var orderFailed = BuildOrder(userA, "Failed", now.AddHours(-3));
            var orderOld = BuildOrder(userA, "Paid", now.AddHours(-48));
            var orderVeryOld = BuildOrder(userA, "Paid", now.AddHours(-500));

            var submittedApplication = new ShopApplication
            {
                OwnerUser = userB,
                Name = "Ops submitted",
                CountryTag = "RDC",
                City = "Kinshasa",
                Status = "Submitted"
            };

            var draftApplication = new ShopApplication
            {
                OwnerUser = userC,
                Name = "Ops draft",
                CountryTag = "RDC",
                City = "Kinshasa",
                Status = "Draft"
            };

            var payoutDraft = new ShopPayout
            {
                Shop = shop,
                Currency = "USD",
                PeriodStartUtc = now.AddDays(-7),
                PeriodEndUtc = now.AddDays(-1),
                GrossSalesItems = 100m,
                PlatformFee = 10m,
                NetToPay = 80m,
                DeliveryRevenue = 0m,
                AmountPaid = 0m,
                Status = "Draft"
            };

            var payoutApproved = new ShopPayout
            {
                Shop = shop,
                Currency = "USD",
                PeriodStartUtc = now.AddDays(-15),
                PeriodEndUtc = now.AddDays(-8),
                GrossSalesItems = 50m,
                PlatformFee = 5m,
                NetToPay = 20m,
                DeliveryRevenue = 0m,
                AmountPaid = 0m,
                Status = "Approved"
            };

            var payoutPaid = new ShopPayout
            {
                Shop = shop,
                Currency = "USD",
                PeriodStartUtc = now.AddDays(-30),
                PeriodEndUtc = now.AddDays(-20),
                GrossSalesItems = 40m,
                PlatformFee = 4m,
                NetToPay = 15m,
                DeliveryRevenue = 0m,
                AmountPaid = 15m,
                Status = "Paid"
            };

            var liveBanner = new MarketingBanner
            {
                Title = "Ops live",
                IsActive = true,
                StartAtUtc = now.AddDays(-1),
                EndAtUtc = now.AddDays(1)
            };

            var inactiveBanner = new MarketingBanner
            {
                Title = "Ops inactive",
                IsActive = false,
                StartAtUtc = now.AddDays(-1),
                EndAtUtc = now.AddDays(1)
            };

            var expiredBanner = new MarketingBanner
            {
                Title = "Ops expired",
                IsActive = true,
                StartAtUtc = now.AddDays(-3),
                EndAtUtc = now.AddDays(-1)
            };

            var logResolvedFailed = new TransactionalNotificationLog
            {
                OrderId = orderPaid.Id,
                NotificationType = NotificationEvents.ClientPaymentPaid,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = userA.Email,
                Subject = "resolved failed",
                Status = NotificationEvents.StatusFailed,
                AttemptedAtUtc = now.AddHours(-2)
            };

            var logResolvedSent = new TransactionalNotificationLog
            {
                OrderId = orderPaid.Id,
                NotificationType = NotificationEvents.ClientPaymentPaid,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = userA.Email,
                Subject = "resolved sent",
                Status = NotificationEvents.StatusSent,
                AttemptedAtUtc = now.AddHours(-1)
            };

            var logUnresolvedFailedWindow = new TransactionalNotificationLog
            {
                OrderId = orderFailed.Id,
                NotificationType = NotificationEvents.ClientPaymentFailed,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = userA.Email,
                Subject = "unresolved failed recent",
                Status = NotificationEvents.StatusFailed,
                AttemptedAtUtc = now.AddHours(-3)
            };

            var logUnresolvedFailedWindowSecond = new TransactionalNotificationLog
            {
                OrderId = Guid.NewGuid(),
                NotificationType = NotificationEvents.AdminOrderPaid,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = "admin@door-market.com",
                Subject = "admin failed recent",
                Status = NotificationEvents.StatusFailed,
                AttemptedAtUtc = now.AddHours(-4)
            };

            var logUnresolvedFailedOld = new TransactionalNotificationLog
            {
                OrderId = orderVeryOld.Id,
                NotificationType = NotificationEvents.ShopOrderPaidPending,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = owner.Email,
                Subject = "shop failed old",
                Status = NotificationEvents.StatusFailed,
                AttemptedAtUtc = now.AddHours(-500)
            };

            var logOldSent = new TransactionalNotificationLog
            {
                OrderId = orderVeryOld.Id,
                NotificationType = NotificationEvents.ShopOrderPaidPending,
                Channel = NotificationEvents.ChannelEmail,
                Recipient = owner.Email,
                Subject = "shop sent old",
                Status = NotificationEvents.StatusSent,
                AttemptedAtUtc = now.AddHours(-501)
            };

            db.AddRange(
                userA, userB, userC, owner,
                category, shop,
                productOut, productLow, productHealthy,
                cartA, cartB, cartC,
                cartItemA, cartItemB, cartItemC,
                orderPaid, orderFailed, orderOld, orderVeryOld,
                submittedApplication, draftApplication,
                payoutDraft, payoutApproved, payoutPaid,
                liveBanner, inactiveBanner, expiredBanner,
                logResolvedFailed, logResolvedSent,
                logUnresolvedFailedWindow, logUnresolvedFailedWindowSecond,
                logUnresolvedFailedOld, logOldSent);
            await db.SaveChangesAsync();

            return new OperationsFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static Order BuildOrder(User user, string paymentStatus, DateTime createdAtUtc)
            => new()
            {
                User = user,
                Status = paymentStatus == "Paid" ? "Paid" : "Created",
                PaymentStatus = paymentStatus,
                FulfillmentStatus = paymentStatus == "Paid" ? "PaidPending" : "PendingPayment",
                PaymentProvider = "PayPal",
                Currency = "USD",
                Subtotal = 10m,
                TotalItemsAmount = 10m,
                PlatformFeeTotal = 1m,
                DeliveryFee = 0m,
                Discount = 0m,
                TotalAmount = 10m,
                DeliveryName = "Client",
                DeliveryPhone = "+2430000000",
                DeliveryLine1 = "Address",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                CreatedAtUtc = createdAtUtc
            };
    }
}
