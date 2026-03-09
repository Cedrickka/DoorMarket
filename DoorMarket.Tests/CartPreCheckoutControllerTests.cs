using DoorMarket.Api.Controllers;
using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Carts;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class CartPreCheckoutControllerTests
{
    [Fact]
    public async Task PreCheckout_WithReadyCart_ReturnsExpectedTotalsAndReadyTrue()
    {
        await using var fixture = await CartFixture.CreateAsync();
        var controller = fixture.CreateController(fixture.ClientUserId);

        var result = await controller.PreCheckout(
            new PreCheckoutRequest(
                DeliveryZoneId: fixture.ZoneId,
                PromoCode: "door10",
                PaymentProvider: "paypal",
                PrepaidCardCode: null,
                RequireDeliveryZone: true),
            CancellationToken.None);

        var payload = AssertOk(result);
        Assert.True(payload.IsReady);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(2, payload.ItemCount);
        Assert.Equal(40m, payload.Subtotal);
        Assert.Equal(4m, payload.Discount);
        Assert.Equal(5m, payload.DeliveryFee);
        Assert.Equal(41m, payload.TotalEstimate);
        Assert.Equal("PayPal", payload.PaymentProvider);
        Assert.Empty(payload.BlockingIssues);
        Assert.Empty(payload.Warnings);
    }

    [Fact]
    public async Task PreCheckout_WithMixedCurrencyAndMissingPrepaidCode_ReturnsBlockingIssues()
    {
        await using var fixture = await CartFixture.CreateAsync();
        await fixture.AddMixedCurrencyItemAsync();
        var controller = fixture.CreateController(fixture.ClientUserId);

        var result = await controller.PreCheckout(
            new PreCheckoutRequest(
                DeliveryZoneId: null,
                PromoCode: null,
                PaymentProvider: "prepaidcard",
                PrepaidCardCode: null,
                RequireDeliveryZone: true),
            CancellationToken.None);

        var payload = AssertOk(result);
        Assert.False(payload.IsReady);
        Assert.Contains(payload.BlockingIssues, x => x.Code == "mixed_currency");
        Assert.Contains(payload.BlockingIssues, x => x.Code == "delivery_zone_required");
        Assert.Contains(payload.BlockingIssues, x => x.Code == "prepaid_code_required");
        Assert.Equal("PrepaidCard", payload.PaymentProvider);
    }

    [Fact]
    public async Task PreCheckout_WithMissingCart_ReturnsEmptyCartIssue()
    {
        await using var fixture = await CartFixture.CreateAsync();
        var missingUserId = Guid.NewGuid();
        var controller = fixture.CreateController(missingUserId);

        var result = await controller.PreCheckout(req: null, CancellationToken.None);

        var payload = AssertOk(result);
        Assert.False(payload.IsReady);
        Assert.Equal("USD", payload.Currency);
        Assert.Equal(0, payload.ItemCount);
        Assert.Contains(payload.BlockingIssues, x => x.Code == "empty_cart");
    }

    [Fact]
    public async Task GetRecoveryStatus_WithSentEvent_ReturnsActiveReminder()
    {
        await using var fixture = await CartFixture.CreateAsync();
        fixture.Db.AbandonedCartEvents.Add(new AbandonedCartEvent
        {
            UserId = fixture.ClientUserId,
            CartId = fixture.CartId,
            Currency = "USD",
            ItemCount = 2,
            Subtotal = 40m,
            LastCartActivityAtUtc = DateTime.UtcNow.AddHours(-3),
            DetectedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExperimentGroup = "A",
            ReminderStatus = "Sent",
            ReminderAttemptCount = 1,
            SentChannels = "Email",
            RecipientEmail = "client-precheckout@test.local",
            ReminderSentAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController(fixture.ClientUserId);
        var result = await controller.GetRecoveryStatus(lookbackDays: 30, CancellationToken.None);

        var payload = AssertOkRecovery(result);
        Assert.True(payload.HasActiveReminder);
        Assert.Equal("Sent", payload.ReminderStatus);
        Assert.Equal("/checkout", payload.CheckoutPath);
        Assert.Equal(2, payload.ItemCount);
        Assert.Equal(40m, payload.Subtotal);
    }

    [Fact]
    public async Task GetRecoveryStatus_WithSkippedEvent_ReturnsInactiveReminder()
    {
        await using var fixture = await CartFixture.CreateAsync();
        fixture.Db.AbandonedCartEvents.Add(new AbandonedCartEvent
        {
            UserId = fixture.ClientUserId,
            CartId = fixture.CartId,
            Currency = "USD",
            ItemCount = 2,
            Subtotal = 40m,
            LastCartActivityAtUtc = DateTime.UtcNow.AddHours(-3),
            DetectedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExperimentGroup = "B",
            ReminderStatus = "Skipped",
            ReminderAttemptCount = 0,
            Error = "Holdout group (B): reminder skipped."
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController(fixture.ClientUserId);
        var result = await controller.GetRecoveryStatus(lookbackDays: 30, CancellationToken.None);

        var payload = AssertOkRecovery(result);
        Assert.False(payload.HasActiveReminder);
        Assert.Equal("Skipped", payload.ReminderStatus);
        Assert.Equal("B", payload.ExperimentGroup);
    }

    private static CheckoutReadinessDto AssertOk(ActionResult<CheckoutReadinessDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<CheckoutReadinessDto>(ok.Value);
    }

    private static CartRecoveryStatusDto AssertOkRecovery(ActionResult<CartRecoveryStatusDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<CartRecoveryStatusDto>(ok.Value);
    }

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

    private sealed class NoopCartService : ICartService
    {
        public Task<CartDto> GetMyCartAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CartDto> UpdateItemAsync(Guid cartItemId, int qty, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class CartFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CartFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            Guid clientUserId,
            Guid ownerUserId,
            Guid shopId,
            Guid categoryId,
            Guid cartId,
            Guid zoneId)
        {
            _connection = connection;
            Db = db;
            ClientUserId = clientUserId;
            OwnerUserId = ownerUserId;
            ShopId = shopId;
            CategoryId = categoryId;
            CartId = cartId;
            ZoneId = zoneId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ClientUserId { get; }
        public Guid OwnerUserId { get; }
        public Guid ShopId { get; }
        public Guid CategoryId { get; }
        public Guid CartId { get; }
        public Guid ZoneId { get; }

        public CartController CreateController(Guid currentUserId)
            => new(
                new NoopCartService(),
                new PromoService(),
                new TestCurrentUserService(currentUserId),
                Db);

        public async Task AddMixedCurrencyItemAsync()
        {
            var product = new Product
            {
                ShopId = ShopId,
                CategoryId = CategoryId,
                Name = "Produit CDF",
                Price = 10000m,
                Currency = "CDF",
                StockQty = 10,
                IsActive = true
            };

            var item = new CartItem
            {
                CartId = CartId,
                Product = product,
                Qty = 1,
                UnitPrice = 10000m
            };

            Db.AddRange(product, item);
            await Db.SaveChangesAsync();
        }

        public static async Task<CartFixture> CreateAsync()
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
                Email = "client-precheckout@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var ownerUser = new User
            {
                Email = "owner-precheckout@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Epicerie",
                Slug = "epicerie-precheckout"
            };

            var shop = new Shop
            {
                OwnerUser = ownerUser,
                Name = "Shop precheckout",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Produit USD",
                Price = 20m,
                Currency = "USD",
                StockQty = 10,
                IsActive = true
            };

            var cart = new Cart
            {
                User = clientUser
            };

            var item = new CartItem
            {
                Cart = cart,
                Product = product,
                Qty = 2,
                UnitPrice = 20m
            };

            var zone = new DeliveryZone
            {
                Code = "KIN-CENTRE-PRE",
                Name = "Centre",
                Country = "CD",
                FeeUsd = 5m,
                IsActive = true
            };

            db.AddRange(clientUser, ownerUser, category, shop, product, cart, item, zone);
            await db.SaveChangesAsync();

            return new CartFixture(connection, db, clientUser.Id, ownerUser.Id, shop.Id, category.Id, cart.Id, zone.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
