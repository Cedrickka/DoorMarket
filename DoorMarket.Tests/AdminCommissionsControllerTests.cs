using DoorMarket.Api.Controllers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public sealed class AdminCommissionsControllerTests
{
    [Fact]
    public async Task CreateRule_WithInvalidScopePayload_ReturnsBadRequest()
    {
        await using var fixture = await CommissionFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.CreateRule(
            new AdminCommissionsController.UpsertCommissionRuleRequest(
                Name: "Invalid shop rule",
                ScopeType: "Shop",
                ScopeShopId: null,
                ScopeCategoryId: null,
                ScopeProductId: null,
                Currency: "USD",
                PlatformFeeMode: "Flat",
                PlatformFeeAmount: 2m,
                PlatformFeePercent: null,
                MinUnitPrice: null,
                MaxUnitPrice: null,
                StartsAtUtc: null,
                EndsAtUtc: null,
                Priority: 100,
                IsActive: true,
                Description: null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ResolvePreview_WithMatchingProductRule_ReturnsDynamicRuleFee()
    {
        await using var fixture = await CommissionFixture.CreateAsync();
        fixture.Db.CommissionRules.Add(new CommissionRule
        {
            Name = "Product percent 15",
            ScopeType = "Product",
            ScopeProductId = fixture.ProductId,
            Currency = "USD",
            PlatformFeeMode = "Percent",
            PlatformFeeAmount = 0m,
            PlatformFeePercent = 15m,
            Priority = 10,
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.ResolvePreview(fixture.ProductId, 20m, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminCommissionsController.CommissionResolvePreviewDto>(ok.Value);
        Assert.Equal("DynamicRule", payload.Source);
        Assert.Equal(3m, payload.AppliedFee);
        Assert.Equal("Product", payload.RuleScopeType);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Email => "admin-comm@test.local";
        public string? Role => "Admin";
        public bool IsAuthenticated => true;
    }

    private sealed class CommissionFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private CommissionFixture(SqliteConnection connection, DoorMarketDbContext db, Guid adminUserId, Guid productId)
        {
            _connection = connection;
            Db = db;
            AdminUserId = adminUserId;
            ProductId = productId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid AdminUserId { get; }
        public Guid ProductId { get; }

        public AdminCommissionsController CreateController()
            => new(Db, new TestCurrentUserService(AdminUserId));

        public static async Task<CommissionFixture> CreateAsync()
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
                Email = "admin-comm@test.local",
                PasswordHash = "hash",
                Role = UserRole.Admin
            };

            var ownerUser = new User
            {
                Email = "owner-comm@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Category commission",
                Slug = "category-commission"
            };

            var shop = new Shop
            {
                OwnerUser = ownerUser,
                Name = "Shop commission",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Product commission",
                Price = 20m,
                Currency = "USD",
                StockQty = 20,
                IsActive = true
            };

            db.AddRange(adminUser, ownerUser, category, shop, product);
            await db.SaveChangesAsync();

            return new CommissionFixture(connection, db, adminUser.Id, product.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
