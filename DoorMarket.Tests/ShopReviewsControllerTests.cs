using DoorMarket.Api.Controllers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class ShopReviewsControllerTests
{
    [Fact]
    public async Task Summary_ReturnsAggregates_ForShop()
    {
        await using var fixture = await ShopReviewsFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.Summary(fixture.ShopId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DoorMarket.Application.DTOs.Shops.ShopReviewSummaryDto>(ok.Value);
        Assert.Equal(fixture.ShopId, dto.ShopId);
        Assert.Equal(2, dto.ReviewCount);
        Assert.Equal(1, dto.Count5);
        Assert.Equal(4, dto.AverageRating);
    }

    [Fact]
    public async Task Create_WhenReviewAlreadyExists_UpdatesWithoutCreatingDuplicate()
    {
        await using var fixture = await ShopReviewsFixture.CreateAsync();
        var controller = fixture.CreateController();

        var first = await controller.Create(
            fixture.ShopId,
            new DoorMarket.Application.DTOs.Shops.CreateShopReviewRequest(5, "First", null, null, null),
            CancellationToken.None);

        var firstOk = Assert.IsType<OkObjectResult>(first.Result);
        var firstDto = Assert.IsType<DoorMarket.Application.DTOs.Shops.ShopReviewDto>(firstOk.Value);

        var second = await controller.Create(
            fixture.ShopId,
            new DoorMarket.Application.DTOs.Shops.CreateShopReviewRequest(2, "Updated", null, null, null),
            CancellationToken.None);

        var secondOk = Assert.IsType<OkObjectResult>(second.Result);
        var secondDto = Assert.IsType<DoorMarket.Application.DTOs.Shops.ShopReviewDto>(secondOk.Value);

        Assert.Equal(firstDto.Id, secondDto.Id);

        var reviews = await fixture.Db.ShopReviews
            .Where(x => x.ShopId == fixture.ShopId && x.UserId == fixture.CurrentUserId)
            .ToListAsync();
        Assert.Single(reviews);
        Assert.Equal(2, reviews[0].Rating);
        Assert.Equal("Updated", reviews[0].Comment);
    }

    [Fact]
    public async Task Create_WhenCommentIsTooLong_ReturnsBadRequest()
    {
        await using var fixture = await ShopReviewsFixture.CreateAsync();
        var controller = fixture.CreateController();
        var comment = new string('x', 1001);

        var result = await controller.Create(
            fixture.ShopId,
            new DoorMarket.Application.DTOs.Shops.CreateShopReviewRequest(4, comment, null, null, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_WhenUserHasDeliveredPaidOrder_MarksReviewAsVerifiedPurchase()
    {
        await using var fixture = await ShopReviewsFixture.CreateAsync();
        await fixture.SeedDeliveredPaidOrderForCurrentUserAsync();
        var controller = fixture.CreateController();

        var result = await controller.Create(
            fixture.ShopId,
            new DoorMarket.Application.DTOs.Shops.CreateShopReviewRequest(5, "Verified", null, null, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DoorMarket.Application.DTOs.Shops.ShopReviewDto>(ok.Value);

        Assert.True(dto.IsVerifiedPurchase);

        var row = await fixture.Db.ShopReviews
            .AsNoTracking()
            .FirstAsync(x => x.ShopId == fixture.ShopId && x.UserId == fixture.CurrentUserId);
        Assert.True(row.IsVerifiedPurchase);
    }

    [Fact]
    public async Task Create_WithInvalidOrderIdForVerification_ReturnsBadRequest()
    {
        await using var fixture = await ShopReviewsFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.Create(
            fixture.ShopId,
            new DoorMarket.Application.DTOs.Shops.CreateShopReviewRequest(4, "Try", Guid.NewGuid(), null, null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private sealed class ShopReviewsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestCurrentUserService _current;

        private ShopReviewsFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            TestCurrentUserService current,
            Guid shopId,
            Guid productId)
        {
            _connection = connection;
            Db = db;
            _current = current;
            ShopId = shopId;
            ProductId = productId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ShopId { get; }
        public Guid ProductId { get; }
        public Guid CurrentUserId => _current.UserId!.Value;

        public ShopReviewsController CreateController() => new(Db, _current);

        public static async Task<ShopReviewsFixture> CreateAsync()
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
                Email = "owner-reviews@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };
            var reviewer = new User
            {
                Email = "client-reviews@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };
            var otherReviewer = new User
            {
                Email = "other-reviews@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };
            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "Review Shop",
                CountryTag = "CD",
                City = "Kinshasa",
                IsVerified = true
            };
            var category = new Category
            {
                Name = "Electromenager",
                Slug = "electromenager"
            };
            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Mixeur",
                Price = 120m,
                Currency = "USD",
                StockQty = 10,
                IsActive = true
            };

            db.AddRange(owner, reviewer, otherReviewer, shop, category, product);
            db.ShopReviews.AddRange(
                new ShopReview
                {
                    Shop = shop,
                    User = reviewer,
                    Rating = 5,
                    Comment = "Great"
                },
                new ShopReview
                {
                    Shop = shop,
                    User = otherReviewer,
                    Rating = 3,
                    Comment = "Okay"
                });
            await db.SaveChangesAsync();

            var current = new TestCurrentUserService(reviewer.Id, reviewer.Email, "Client");
            return new ShopReviewsFixture(connection, db, current, shop.Id, product.Id);
        }

        public async Task SeedDeliveredPaidOrderForCurrentUserAsync()
        {
            var order = new Order
            {
                UserId = CurrentUserId,
                Status = "Delivered",
                PaymentStatus = "Paid",
                FulfillmentStatus = "Delivered",
                DeliveryName = "Test Client",
                DeliveryPhone = "000000000",
                DeliveryLine1 = "Street 1",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                Subtotal = 120m,
                DeliveryFee = 0m,
                Discount = 0m,
                TotalItemsAmount = 120m,
                TotalAmount = 120m,
                Currency = "USD",
                PaidAtUtc = DateTime.UtcNow.AddDays(-1),
                DeliveredAtUtc = DateTime.UtcNow
            };

            var item = new OrderItem
            {
                Order = order,
                ProductId = ProductId,
                Qty = 1,
                UnitPrice = 120m,
                UnitPriceAtPurchase = 120m,
                PlatformFeeAtPurchase = 0m,
                LineTotal = 120m
            };

            Db.Orders.Add(order);
            Db.OrderItems.Add(item);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid? userId, string? email, string? role)
        {
            UserId = userId;
            Email = email;
            Role = role;
        }

        public Guid? UserId { get; }
        public string? Email { get; }
        public string? Role { get; }
        public bool IsAuthenticated => UserId.HasValue;
    }
}
