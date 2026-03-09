using DoorMarket.Api.Services;
using DoorMarket.Application.Interfaces.Loyalty;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorMarket.Tests;

public class OrderPaymentWorkflowServiceTests
{
    [Fact]
    public async Task MarkOrderPaidAsync_SecondCall_DoesNotSendClientPaidTwice()
    {
        await using var fixture = await WorkflowFixture.CreateAsync(includeStockItem: false, insufficientStock: false, initialPaymentStatus: "Pending");
        var workflow = fixture.CreateWorkflow();

        await workflow.MarkOrderPaidAsync(fixture.OrderId, "Stripe", snapshot: null, CancellationToken.None);
        await workflow.MarkOrderPaidAsync(fixture.OrderId, "Stripe", snapshot: null, CancellationToken.None);

        Assert.Equal(1, fixture.ClientNotifications.PaidCount);
    }

    [Fact]
    public async Task MarkOrderFailedAsync_SecondCall_DoesNotSendClientFailedTwice()
    {
        await using var fixture = await WorkflowFixture.CreateAsync(includeStockItem: false, insufficientStock: false, initialPaymentStatus: "Pending");
        var workflow = fixture.CreateWorkflow();

        await workflow.MarkOrderFailedAsync(fixture.OrderId, CancellationToken.None);
        await workflow.MarkOrderFailedAsync(fixture.OrderId, CancellationToken.None);

        Assert.Equal(1, fixture.ClientNotifications.FailedCount);
    }

    [Fact]
    public async Task MarkOrderPaidAsync_StockInsufficient_SendsClientFailedOnlyOnce()
    {
        await using var fixture = await WorkflowFixture.CreateAsync(includeStockItem: true, insufficientStock: true, initialPaymentStatus: "Pending");
        var workflow = fixture.CreateWorkflow();

        var first = await workflow.MarkOrderPaidAsync(fixture.OrderId, "Stripe", snapshot: null, CancellationToken.None);
        var second = await workflow.MarkOrderPaidAsync(fixture.OrderId, "Stripe", snapshot: null, CancellationToken.None);

        Assert.False(first.Paid);
        Assert.False(second.Paid);
        Assert.Equal("Failed", first.PaymentStatus);
        Assert.Equal(1, fixture.ClientNotifications.FailedCount);
    }

    private sealed class WorkflowFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WorkflowFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            Guid orderId,
            FakeOrderNotificationService shopNotifications,
            FakeAdminOrderNotificationService adminNotifications,
            FakeClientOrderNotificationService clientNotifications)
        {
            _connection = connection;
            Db = db;
            OrderId = orderId;
            ShopNotifications = shopNotifications;
            AdminNotifications = adminNotifications;
            ClientNotifications = clientNotifications;
        }

        public DoorMarketDbContext Db { get; }
        public Guid OrderId { get; }
        public FakeOrderNotificationService ShopNotifications { get; }
        public FakeAdminOrderNotificationService AdminNotifications { get; }
        public FakeClientOrderNotificationService ClientNotifications { get; }

        public OrderPaymentWorkflowService CreateWorkflow()
            => new(
                Db,
                ShopNotifications,
                AdminNotifications,
                ClientNotifications,
                new FakeLoyaltyService(),
                NullLogger<OrderPaymentWorkflowService>.Instance);

        public static async Task<WorkflowFixture> CreateAsync(bool includeStockItem, bool insufficientStock, string initialPaymentStatus)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var client = new User
            {
                Email = "client-workflow@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var order = new Order
            {
                User = client,
                Status = "Created",
                PaymentStatus = initialPaymentStatus,
                FulfillmentStatus = "PendingPayment",
                PaymentProvider = "Stripe",
                Currency = "USD",
                Subtotal = 10m,
                TotalItemsAmount = 10m,
                PlatformFeeTotal = 1m,
                DeliveryFee = 2m,
                Discount = 0m,
                TotalAmount = 12m,
                DeliveryName = "Client Workflow",
                DeliveryPhone = "+243000000000",
                DeliveryLine1 = "Address",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD"
            };

            db.Users.Add(client);
            db.Orders.Add(order);

            if (includeStockItem)
            {
                var owner = new User
                {
                    Email = "owner-workflow@test.local",
                    PasswordHash = "hash",
                    Role = UserRole.Shop
                };

                var category = new Category
                {
                    Name = "Cat workflow",
                    Slug = "cat-workflow"
                };

                var shop = new Shop
                {
                    OwnerUser = owner,
                    Name = "Shop workflow",
                    CountryTag = "RDC",
                    City = "Kinshasa",
                    IsVerified = true
                };

                var product = new Product
                {
                    Shop = shop,
                    Category = category,
                    Name = "Produit workflow",
                    Price = 10m,
                    Currency = "USD",
                    StockQty = insufficientStock ? 0 : 10,
                    IsActive = true
                };

                db.OrderItems.Add(new OrderItem
                {
                    Order = order,
                    Product = product,
                    Qty = 1,
                    UnitPrice = 10m,
                    UnitPriceAtPurchase = 10m,
                    PlatformFeeAtPurchase = 1m,
                    LineTotal = 10m
                });
            }

            await db.SaveChangesAsync();

            return new WorkflowFixture(
                connection,
                db,
                order.Id,
                new FakeOrderNotificationService(),
                new FakeAdminOrderNotificationService(),
                new FakeClientOrderNotificationService());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeOrderNotificationService : IOrderNotificationService
    {
        public int Calls { get; private set; }

        public Task NotifyShopsOrderPaidPendingOnceAsync(Guid orderId, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdminOrderNotificationService : IAdminOrderNotificationService
    {
        public int Calls { get; private set; }

        public Task NotifyAdminOrderPaidOnceAsync(Guid orderId, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientOrderNotificationService : IClientOrderNotificationService
    {
        public int CreatedCount { get; private set; }
        public int PaidCount { get; private set; }
        public int FailedCount { get; private set; }

        public Task NotifyClientOrderCreatedAsync(Guid orderId, CancellationToken ct)
        {
            CreatedCount++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentPaidAsync(Guid orderId, CancellationToken ct)
        {
            PaidCount++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct)
        {
            FailedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLoyaltyService : ILoyaltyService
    {
        public int Calls { get; private set; }

        public Task AwardOrderPaidAsync(Guid orderId, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
