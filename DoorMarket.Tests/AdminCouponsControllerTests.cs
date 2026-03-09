using DoorMarket.Api.Controllers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Carts;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminCouponsControllerTests
{
    [Fact]
    public async Task Create_And_List_Coupon_Works()
    {
        await using var fixture = await CouponsFixture.CreateAsync();
        var controller = fixture.CreateAdminController();

        var create = await controller.Create(
            new AdminCouponsController.UpsertCouponRequest(
                Code: "launch15",
                Name: "Launch 15",
                Description: "Promo lancement",
                IsActive: true,
                DiscountType: "Percent",
                DiscountValue: 15m,
                MaxDiscountAmount: 25m,
                Currency: "usd",
                MinSubtotal: 10m,
                StartsAtUtc: DateTime.UtcNow.AddDays(-1),
                EndsAtUtc: DateTime.UtcNow.AddDays(10),
                BudgetAmount: 500m,
                UsageLimitTotal: 100,
                UsageLimitPerUser: 2,
                ScopeType: "Global",
                ScopeShopId: null,
                ScopeCategoryId: null,
                ScopeCity: null,
                ScopeCountry: null),
            CancellationToken.None);

        var okCreate = Assert.IsType<OkObjectResult>(create.Result);
        var created = Assert.IsType<AdminCouponsController.CouponDetailDto>(okCreate.Value);
        Assert.Equal("LAUNCH15", created.Code);
        Assert.Equal("Percent", created.DiscountType);
        Assert.Equal(15m, created.DiscountValue);
        Assert.Equal("USD", created.Currency);
        Assert.Equal("Live", created.OperationalStatus);

        var list = await controller.GetCoupons(
            q: "LAUNCH",
            active: true,
            scope: null,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var okList = Assert.IsType<OkObjectResult>(list.Result);
        var page = Assert.IsType<DoorMarket.Application.Common.PagedResult<AdminCouponsController.CouponRowDto>>(okList.Value);
        Assert.Single(page.Items);
        Assert.Equal("LAUNCH15", page.Items[0].Code);
    }

    [Fact]
    public async Task PromoService_UsesCouponFromDatabase_AndHonorsUsageLimit()
    {
        await using var fixture = await CouponsFixture.CreateAsync();

        var coupon = new Coupon
        {
            Code = "LIMIT1",
            Name = "One shot",
            IsActive = true,
            DiscountType = "Percent",
            DiscountValue = 10m,
            ScopeType = "Global",
            UsageLimitTotal = 1
        };

        fixture.Db.Coupons.Add(coupon);
        await fixture.Db.SaveChangesAsync();

        var service = fixture.CreatePromoServiceForClient();
        var first = service.Evaluate("limit1", 40m);
        Assert.True(first.Applied);
        Assert.Equal(4m, first.Discount);
        Assert.Equal("LIMIT1", first.PromoCode);

        fixture.Db.PromoAuditLogs.Add(new PromoAuditLog
        {
            UserId = fixture.ClientUserId,
            Source = "Checkout",
            PromoCode = "LIMIT1",
            Subtotal = 40m,
            Discount = 4m,
            Currency = "USD",
            Applied = true,
            Message = "Coupon applique"
        });
        await fixture.Db.SaveChangesAsync();

        var second = service.Evaluate("LIMIT1", 40m);
        Assert.False(second.Applied);
        Assert.Equal(0m, second.Discount);
    }

    private sealed class CouponsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CouponsFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            Guid adminUserId,
            Guid clientUserId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ClientUserId = clientUserId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid AdminUserId { get; }
        public Guid ClientUserId { get; }

        public AdminCouponsController CreateAdminController()
            => new(Db);

        public PromoService CreatePromoServiceForClient()
            => new(Db, new TestCurrentUserService(ClientUserId, "Client"));

        public static async Task<CouponsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var admin = new User
            {
                Email = "admin-coupon@test.local",
                PasswordHash = "hash",
                Role = UserRole.Admin
            };
            var client = new User
            {
                Email = "client-coupon@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            db.AddRange(admin, client);
            await db.SaveChangesAsync();

            return new CouponsFixture(connection, db, admin.Id, client.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId, string role)
        {
            UserId = userId;
            Role = role;
        }

        public Guid? UserId { get; }
        public string? Email => "test@local";
        public string? Role { get; }
        public bool IsAuthenticated => UserId.HasValue;
    }
}
