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

public class CartCouponValidationControllerTests
{
    [Fact]
    public async Task ValidateCoupon_WithValidCoupon_ReturnsAppliedDiscountAndTotal()
    {
        await using var fixture = await CouponValidationFixture.CreateAsync();
        fixture.Db.Coupons.Add(new Coupon
        {
            Code = "SAVE10",
            Name = "Save 10",
            IsActive = true,
            DiscountType = "Percent",
            DiscountValue = 10m,
            ScopeType = "Global"
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController(
            new CartDto(
                CartId: Guid.NewGuid(),
                Items: new List<CartItemDto>
                {
                    new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Prod", 20m, 2, 40m, "USD", null)
                },
                Subtotal: 40m,
                Currency: "USD"));

        var result = await controller.ValidateCoupon(
            new CouponValidationRequest("save10", fixture.ZoneId, true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CouponValidationDto>(ok.Value);
        Assert.True(payload.IsReady);
        Assert.True(payload.Applied);
        Assert.Equal("SAVE10", payload.PromoCode);
        Assert.Equal(4m, payload.Discount);
        Assert.Equal(5m, payload.DeliveryFee);
        Assert.Equal(41m, payload.TotalEstimate);
        Assert.Empty(payload.BlockingIssues);

        var audit = await fixture.Db.PromoAuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync();
        Assert.Equal("CartValidateCoupon", audit.Source);
        Assert.Equal("SAVE10", audit.PromoCode);
        Assert.True(audit.Applied);
    }

    [Fact]
    public async Task ValidateCoupon_WithInvalidCode_ReturnsWarningAndNoDiscount()
    {
        await using var fixture = await CouponValidationFixture.CreateAsync();
        var controller = fixture.CreateController(
            new CartDto(
                CartId: Guid.NewGuid(),
                Items: new List<CartItemDto>
                {
                    new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Prod", 30m, 1, 30m, "USD", null)
                },
                Subtotal: 30m,
                Currency: "USD"));

        var result = await controller.ValidateCoupon(
            new CouponValidationRequest("BADCODE", null, false),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CouponValidationDto>(ok.Value);
        Assert.True(payload.IsReady);
        Assert.False(payload.Applied);
        Assert.Null(payload.PromoCode);
        Assert.Equal(0m, payload.Discount);
        Assert.Single(payload.Warnings);
        Assert.Equal("coupon_not_applied", payload.Warnings[0].Code);
    }

    [Fact]
    public async Task ValidateCoupon_WithMissingCode_ReturnsBadRequest()
    {
        await using var fixture = await CouponValidationFixture.CreateAsync();
        var controller = fixture.CreateController(
            new CartDto(
                CartId: Guid.NewGuid(),
                Items: new List<CartItemDto>(),
                Subtotal: 0m,
                Currency: "USD"));

        var result = await controller.ValidateCoupon(
            new CouponValidationRequest("   ", null, false),
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Code coupon requis", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CouponValidationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestCurrentUserService _current;

        private CouponValidationFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            TestCurrentUserService current,
            Guid zoneId)
        {
            _connection = connection;
            Db = db;
            _current = current;
            ZoneId = zoneId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ZoneId { get; }

        public CartController CreateController(CartDto cart)
            => new(
                new FixedCartService(cart),
                new PromoService(Db, _current),
                _current,
                Db);

        public static async Task<CouponValidationFixture> CreateAsync()
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
                Email = "client-coupon-validate@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var zone = new DeliveryZone
            {
                Code = "KIN-COUPON",
                Name = "Centre",
                Country = "CD",
                FeeUsd = 5m,
                IsActive = true
            };

            db.AddRange(client, zone);
            await db.SaveChangesAsync();

            return new CouponValidationFixture(
                connection,
                db,
                new TestCurrentUserService(client.Id),
                zone.Id);
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
        public string? Email => "client@test.local";
        public string? Role => "Client";
        public bool IsAuthenticated => true;
    }

    private sealed class FixedCartService : ICartService
    {
        private readonly CartDto _cart;

        public FixedCartService(CartDto cart)
        {
            _cart = cart;
        }

        public Task<CartDto> GetMyCartAsync(CancellationToken ct)
            => Task.FromResult(_cart);

        public Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CartDto> UpdateItemAsync(Guid cartItemId, int qty, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task ClearAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}
