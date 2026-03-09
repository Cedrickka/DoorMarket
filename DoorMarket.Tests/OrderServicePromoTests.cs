using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Carts;
using DoorMarket.Infrastructure.Orders;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class OrderServicePromoTests
{
    [Fact]
    public async Task CheckoutAsync_WithValidPromo_AppliesDiscountAndWritesAudit()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var service = new OrderService(
            fixture.Db,
            new TestCurrentUserService(fixture.ClientUserId),
            new PromoService());

        var order = await service.CheckoutAsync(
            BuildRequest(fixture.ZoneId, "DOOR10"),
            CancellationToken.None);

        Assert.Equal(40m, order.Subtotal);
        Assert.Equal(4m, order.Discount);
        Assert.Equal(41m, order.TotalAmount);

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal(4m, persistedOrder.Discount);
        Assert.Equal("DOOR10", persistedOrder.PromoCode);

        var audit = await fixture.Db.PromoAuditLogs.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal("Checkout", audit.Source);
        Assert.True(audit.Applied);
        Assert.Equal(4m, audit.Discount);
        Assert.Equal("DOOR10", audit.PromoCode);
    }

    [Fact]
    public async Task CheckoutAsync_WithInvalidPromo_DoesNotDiscountAndKeepsPromoCodeNull()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var service = new OrderService(
            fixture.Db,
            new TestCurrentUserService(fixture.ClientUserId),
            new PromoService());

        var order = await service.CheckoutAsync(
            BuildRequest(fixture.ZoneId, "BADCODE"),
            CancellationToken.None);

        Assert.Equal(0m, order.Discount);
        Assert.Equal(45m, order.TotalAmount);

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Null(persistedOrder.PromoCode);

        var audit = await fixture.Db.PromoAuditLogs.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.False(audit.Applied);
        Assert.Equal(0m, audit.Discount);
        Assert.Equal("BADCODE", audit.PromoCode);
    }

    [Fact]
    public async Task CheckoutAsync_WithPercentCommission_ComputesPlatformFeeFromPercent()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();

        var product = await fixture.Db.Products.SingleAsync();
        product.PlatformFeeMode = "Percent";
        product.PlatformFeePercent = 10m;
        product.PlatformFeeAmount = 0m;
        await fixture.Db.SaveChangesAsync();

        var service = new OrderService(
            fixture.Db,
            new TestCurrentUserService(fixture.ClientUserId),
            new PromoService());

        var order = await service.CheckoutAsync(
            BuildRequest(fixture.ZoneId, string.Empty),
            CancellationToken.None);

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal(4m, persistedOrder.PlatformFeeTotal);

        var item = await fixture.Db.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(2m, item.PlatformFeeAtPurchase);
    }

    [Fact]
    public async Task CheckoutAsync_WithDynamicGlobalCommissionRule_OverridesProductDefaultFee()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();

        fixture.Db.CommissionRules.Add(new CommissionRule
        {
            Name = "Global USD 20%",
            ScopeType = "Global",
            Currency = "USD",
            PlatformFeeMode = "Percent",
            PlatformFeePercent = 20m,
            PlatformFeeAmount = 0m,
            Priority = 100,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();

        var service = new OrderService(
            fixture.Db,
            new TestCurrentUserService(fixture.ClientUserId),
            new PromoService());

        var order = await service.CheckoutAsync(
            BuildRequest(fixture.ZoneId, string.Empty),
            CancellationToken.None);

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal(8m, persistedOrder.PlatformFeeTotal);

        var item = await fixture.Db.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(4m, item.PlatformFeeAtPurchase);
    }

    [Fact]
    public async Task CheckoutAsync_WithProductDynamicRule_PrioritizesMostSpecificScope()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var product = await fixture.Db.Products.SingleAsync();

        fixture.Db.CommissionRules.Add(new CommissionRule
        {
            Name = "Global 5%",
            ScopeType = "Global",
            Currency = "USD",
            PlatformFeeMode = "Percent",
            PlatformFeePercent = 5m,
            PlatformFeeAmount = 0m,
            Priority = 100,
            IsActive = true
        });
        fixture.Db.CommissionRules.Add(new CommissionRule
        {
            Name = "Product fixed 6",
            ScopeType = "Product",
            ScopeProductId = product.Id,
            Currency = "USD",
            PlatformFeeMode = "Flat",
            PlatformFeeAmount = 6m,
            PlatformFeePercent = null,
            Priority = 100,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();

        var service = new OrderService(
            fixture.Db,
            new TestCurrentUserService(fixture.ClientUserId),
            new PromoService());

        var order = await service.CheckoutAsync(
            BuildRequest(fixture.ZoneId, string.Empty),
            CancellationToken.None);

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal(12m, persistedOrder.PlatformFeeTotal);

        var item = await fixture.Db.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(6m, item.PlatformFeeAtPurchase);
    }

    private static CheckoutRequest BuildRequest(Guid zoneId, string promoCode)
        => new(
            DeliveryName: "Client Test",
            DeliveryPhone: "+2430000001",
            DeliveryLine1: "Avenue Test",
            DeliveryCity: "Kinshasa",
            DeliveryCountry: "CD",
            DeliveryZoneId: zoneId,
            DeliveryNotes: null,
            PromoCode: promoCode,
            PaymentProvider: "PayPal",
            PrepaidCardCode: null);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Email => "client@test.local";
        public string? Role => "Client";
        public bool IsAuthenticated => true;
    }

    private sealed class CheckoutFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CheckoutFixture(SqliteConnection connection, DoorMarketDbContext db, Guid clientUserId, Guid zoneId)
        {
            _connection = connection;
            Db = db;
            ClientUserId = clientUserId;
            ZoneId = zoneId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ClientUserId { get; }
        public Guid ZoneId { get; }

        public static async Task<CheckoutFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var clientUser = new User
            {
                Email = "client@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var ownerUser = new User
            {
                Email = "owner@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Epicerie",
                Slug = "epicerie"
            };

            var shop = new Shop
            {
                OwnerUser = ownerUser,
                Name = "Shop Test",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Produit Test",
                Price = 20m,
                Currency = "USD",
                StockQty = 10,
                IsActive = true
            };

            var cart = new Cart
            {
                User = clientUser
            };

            var cartItem = new CartItem
            {
                Cart = cart,
                Product = product,
                Qty = 2,
                UnitPrice = 20m
            };

            var zone = new DeliveryZone
            {
                Code = "KIN-CENTRE",
                Name = "Centre",
                Country = "CD",
                FeeUsd = 5m,
                IsActive = true
            };

            db.AddRange(clientUser, ownerUser, category, shop, product, cart, cartItem, zone);
            await db.SaveChangesAsync();

            return new CheckoutFixture(connection, db, clientUser.Id, zone.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
