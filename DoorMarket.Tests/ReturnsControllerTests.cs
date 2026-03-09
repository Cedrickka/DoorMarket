using DoorMarket.Api.Controllers;
using DoorMarket.Application.DTOs.Returns;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class ReturnsControllerTests
{
    [Fact]
    public async Task Create_WithEligibleOrder_CreatesRequestedReturn()
    {
        await using var fixture = await ReturnsFixture.CreateAsync();
        var controller = fixture.CreateController();

        var result = await controller.Create(
            new CreateReturnRequest(
                fixture.EligibleOrderId,
                null,
                "Produit non conforme",
                "Le produit ne correspond pas a la description",
                40m),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ReturnRequestDto>(created.Value);

        Assert.Equal("Requested", dto.Status);
        Assert.Equal(40m, dto.RequestedAmount);
        Assert.Equal("USD", dto.Currency);
    }

    [Fact]
    public async Task Create_WithAmountAboveRemaining_ReturnsBadRequest()
    {
        await using var fixture = await ReturnsFixture.CreateAsync();
        await fixture.SeedCommittedRefundAsync(80m);
        var controller = fixture.CreateController();

        var result = await controller.Create(
            new CreateReturnRequest(
                fixture.EligibleOrderId,
                null,
                "Article defectueux",
                null,
                30m),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyCurrentUserRows()
    {
        await using var fixture = await ReturnsFixture.CreateAsync();
        await fixture.SeedReturnForCurrentUserAsync();
        await fixture.SeedReturnForOtherUserAsync();
        var controller = fixture.CreateController();

        var result = await controller.Mine(page: 1, pageSize: 20, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DoorMarket.Application.Common.PagedResult<ReturnRequestDto>>(ok.Value);
        Assert.Single(payload.Items);
        Assert.All(payload.Items, x => Assert.Equal(fixture.EligibleOrderId, x.OrderId));
    }

    [Fact]
    public async Task GetById_WhenNotOwner_ReturnsNotFound()
    {
        await using var fixture = await ReturnsFixture.CreateAsync();
        var otherId = await fixture.SeedReturnForOtherUserAsync();
        var controller = fixture.CreateController();

        var result = await controller.GetById(otherId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class ReturnsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestCurrentUserService _current;

        private ReturnsFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            TestCurrentUserService current,
            Guid currentUserId,
            Guid otherUserId,
            Guid eligibleOrderId,
            Guid otherOrderId)
        {
            _connection = connection;
            Db = db;
            _current = current;
            CurrentUserId = currentUserId;
            OtherUserId = otherUserId;
            EligibleOrderId = eligibleOrderId;
            OtherOrderId = otherOrderId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid CurrentUserId { get; }
        public Guid OtherUserId { get; }
        public Guid EligibleOrderId { get; }
        public Guid OtherOrderId { get; }

        public ReturnsController CreateController() => new(Db, _current);

        public static async Task<ReturnsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var currentUser = new User
            {
                Email = "client-returns@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };
            var otherUser = new User
            {
                Email = "other-returns@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var eligibleOrder = BuildDeliveredPaidOrder(currentUser);
            var otherOrder = BuildDeliveredPaidOrder(otherUser);

            db.AddRange(currentUser, otherUser, eligibleOrder, otherOrder);
            await db.SaveChangesAsync();

            var current = new TestCurrentUserService(currentUser.Id, currentUser.Email, "Client");
            return new ReturnsFixture(
                connection,
                db,
                current,
                currentUser.Id,
                otherUser.Id,
                eligibleOrder.Id,
                otherOrder.Id);
        }

        public async Task SeedCommittedRefundAsync(decimal approvedAmount)
        {
            Db.ReturnRequests.Add(new ReturnRequest
            {
                OrderId = EligibleOrderId,
                UserId = CurrentUserId,
                Status = "Refunded",
                Reason = "Historique",
                RequestedAmount = approvedAmount,
                ApprovedAmount = approvedAmount,
                Currency = "USD",
                RefundedAtUtc = DateTime.UtcNow
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Guid> SeedReturnForCurrentUserAsync()
        {
            var row = new ReturnRequest
            {
                OrderId = EligibleOrderId,
                UserId = CurrentUserId,
                Status = "Requested",
                Reason = "Current",
                RequestedAmount = 15m,
                Currency = "USD"
            };
            Db.ReturnRequests.Add(row);
            await Db.SaveChangesAsync();
            return row.Id;
        }

        public async Task<Guid> SeedReturnForOtherUserAsync()
        {
            var row = new ReturnRequest
            {
                OrderId = OtherOrderId,
                UserId = OtherUserId,
                Status = "Requested",
                Reason = "Other",
                RequestedAmount = 12m,
                Currency = "USD"
            };
            Db.ReturnRequests.Add(row);
            await Db.SaveChangesAsync();
            return row.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static Order BuildDeliveredPaidOrder(User user)
            => new()
            {
                User = user,
                Status = "Delivered",
                PaymentStatus = "Paid",
                FulfillmentStatus = "Delivered",
                DeliveryName = "Client",
                DeliveryPhone = "000000000",
                DeliveryLine1 = "Street 1",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                Subtotal = 100m,
                DeliveryFee = 0m,
                Discount = 0m,
                TotalItemsAmount = 100m,
                TotalAmount = 100m,
                Currency = "USD",
                PaidAtUtc = DateTime.UtcNow.AddDays(-1),
                DeliveredAtUtc = DateTime.UtcNow
            };
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
