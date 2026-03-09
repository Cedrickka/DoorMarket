using DoorMarket.Api.Controllers;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminProductsControllerTests
{
    [Fact]
    public async Task BulkUpdateFeesByIds_UpdatesOnlyActive_WhenOnlyActiveTrue()
    {
        await using var fixture = await ProductFixture.CreateAsync();
        var controller = new AdminProductsController(fixture.Db);

        var result = await controller.BulkUpdateFeesByIds(
            new AdminProductsController.BulkUpdateProductFeesRequest(
                ProductIds: fixture.ProductIds,
                PlatformFeeMode: "Percent",
                PlatformFeeAmount: null,
                PlatformFeePercent: 12m,
                OnlyActive: true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminProductsController.BulkUpdateProductFeesResult>(ok.Value);
        Assert.Equal(1, payload.UpdatedCount);
        Assert.Equal(1, payload.SkippedCount);

        var active = await fixture.Db.Products.AsNoTracking().SingleAsync(x => x.Name == "Active product");
        var inactive = await fixture.Db.Products.AsNoTracking().SingleAsync(x => x.Name == "Inactive product");
        Assert.Equal("Percent", active.PlatformFeeMode);
        Assert.Equal(12m, active.PlatformFeePercent);
        Assert.Equal("Flat", inactive.PlatformFeeMode);
    }

    [Fact]
    public async Task BulkUpdateFeesByIds_InvalidPercent_ReturnsBadRequest()
    {
        await using var fixture = await ProductFixture.CreateAsync();
        var controller = new AdminProductsController(fixture.Db);

        var result = await controller.BulkUpdateFeesByIds(
            new AdminProductsController.BulkUpdateProductFeesRequest(
                ProductIds: fixture.ProductIds,
                PlatformFeeMode: "Percent",
                PlatformFeeAmount: null,
                PlatformFeePercent: 0m,
                OnlyActive: false),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private sealed class ProductFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ProductFixture(SqliteConnection connection, DoorMarketDbContext db, List<Guid> productIds)
        {
            _connection = connection;
            Db = db;
            ProductIds = productIds;
        }

        public DoorMarketDbContext Db { get; }
        public List<Guid> ProductIds { get; }

        public static async Task<ProductFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var owner = new User
            {
                Email = "owner-products@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Category products",
                Slug = "cat-products"
            };

            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "Shop products",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var active = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Active product",
                Price = 20m,
                Currency = "USD",
                StockQty = 5,
                IsActive = true,
                PlatformFeeMode = "Flat",
                PlatformFeeAmount = 1m
            };

            var inactive = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Inactive product",
                Price = 30m,
                Currency = "USD",
                StockQty = 5,
                IsActive = false,
                PlatformFeeMode = "Flat",
                PlatformFeeAmount = 2m
            };

            db.AddRange(owner, category, shop, active, inactive);
            await db.SaveChangesAsync();

            return new ProductFixture(connection, db, new List<Guid> { active.Id, inactive.Id });
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
