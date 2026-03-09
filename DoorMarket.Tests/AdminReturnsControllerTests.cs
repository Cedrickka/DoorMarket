using DoorMarket.Api.Controllers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminReturnsControllerTests
{
    [Fact]
    public async Task Approve_RequestedReturn_UpdatesStatusAndAddsHistory()
    {
        await using var fixture = await AdminReturnsFixture.CreateAsync();
        var returnId = await fixture.SeedReturnAsync("Requested", requestedAmount: 80m);
        var controller = fixture.CreateController();

        var result = await controller.Approve(
            returnId,
            new AdminReturnsController.ApproveReturnRequest(55m, "Eligible partiel"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminReturnsController.AdminReturnDetail>(ok.Value);
        Assert.Equal("Approved", dto.Status);
        Assert.Equal(55m, dto.ApprovedAmount);

        var history = await fixture.Db.ReturnRequestStatusHistories.AsNoTracking()
            .Where(x => x.ReturnRequestId == returnId)
            .OrderBy(x => x.ChangedAtUtc)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Requested", history[1].OldStatus);
        Assert.Equal("Approved", history[1].NewStatus);
    }

    [Fact]
    public async Task Reject_RequestedReturn_UpdatesStatusAndAddsHistory()
    {
        await using var fixture = await AdminReturnsFixture.CreateAsync();
        var returnId = await fixture.SeedReturnAsync("Requested", requestedAmount: 40m);
        var controller = fixture.CreateController();

        var result = await controller.Reject(
            returnId,
            new AdminReturnsController.RejectReturnRequest("Hors delai"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminReturnsController.AdminReturnDetail>(ok.Value);
        Assert.Equal("Rejected", dto.Status);
        Assert.Equal(0m, dto.ApprovedAmount);

        var history = await fixture.Db.ReturnRequestStatusHistories.AsNoTracking()
            .Where(x => x.ReturnRequestId == returnId)
            .OrderBy(x => x.ChangedAtUtc)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Requested", history[1].OldStatus);
        Assert.Equal("Rejected", history[1].NewStatus);
    }

    [Fact]
    public async Task MarkRefunded_ApprovedReturn_UpdatesStatusAndAddsHistory()
    {
        await using var fixture = await AdminReturnsFixture.CreateAsync();
        var returnId = await fixture.SeedReturnAsync(
            "Approved",
            requestedAmount: 90m,
            approvedAmount: 70m);
        var controller = fixture.CreateController();

        var result = await controller.MarkRefunded(
            returnId,
            new AdminReturnsController.MarkReturnRefundedRequest(70m, DateTime.UtcNow, "Virement effectue"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminReturnsController.AdminReturnDetail>(ok.Value);
        Assert.Equal("Refunded", dto.Status);
        Assert.Equal(70m, dto.ApprovedAmount);
        Assert.NotNull(dto.RefundedAtUtc);

        var history = await fixture.Db.ReturnRequestStatusHistories.AsNoTracking()
            .Where(x => x.ReturnRequestId == returnId)
            .OrderBy(x => x.ChangedAtUtc)
            .ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Equal("Approved", history[2].OldStatus);
        Assert.Equal("Refunded", history[2].NewStatus);
    }

    [Fact]
    public async Task History_ReturnsJournalRows()
    {
        await using var fixture = await AdminReturnsFixture.CreateAsync();
        var returnId = await fixture.SeedReturnAsync(
            "Approved",
            requestedAmount: 60m,
            approvedAmount: 60m);
        var controller = fixture.CreateController();

        var result = await controller.GetHistory(returnId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<AdminReturnsController.AdminReturnHistoryRow>>(ok.Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Approved", rows[0].NewStatus);
    }

    private sealed class AdminReturnsFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestCurrentUserService _current;

        private AdminReturnsFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            TestCurrentUserService current,
            Guid clientUserId,
            Guid orderId)
        {
            _connection = connection;
            Db = db;
            _current = current;
            ClientUserId = clientUserId;
            OrderId = orderId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ClientUserId { get; }
        public Guid OrderId { get; }

        public AdminReturnsController CreateController() => new(Db, _current);

        public static async Task<AdminReturnsFixture> CreateAsync()
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
                Email = "admin-returns@test.local",
                PasswordHash = "hash",
                Role = UserRole.Admin
            };
            var client = new User
            {
                Email = "client-returns@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var order = new Order
            {
                User = client,
                Status = "Delivered",
                PaymentStatus = "Paid",
                FulfillmentStatus = "Delivered",
                DeliveryName = "Client Name",
                DeliveryPhone = "0999999999",
                DeliveryLine1 = "Line 1",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                Subtotal = 100m,
                DeliveryFee = 0m,
                Discount = 0m,
                TotalItemsAmount = 100m,
                TotalAmount = 100m,
                Currency = "USD",
                PaidAtUtc = DateTime.UtcNow.AddDays(-2),
                DeliveredAtUtc = DateTime.UtcNow.AddDays(-1)
            };

            db.AddRange(admin, client, order);
            await db.SaveChangesAsync();

            var current = new TestCurrentUserService(admin.Id, admin.Email, "Admin");
            return new AdminReturnsFixture(connection, db, current, client.Id, order.Id);
        }

        public async Task<Guid> SeedReturnAsync(
            string status,
            decimal requestedAmount,
            decimal? approvedAmount = null)
        {
            var now = DateTime.UtcNow;
            var row = new ReturnRequest
            {
                OrderId = OrderId,
                UserId = ClientUserId,
                Status = status,
                Reason = "Test reason",
                RequestedAmount = requestedAmount,
                ApprovedAmount = approvedAmount,
                Currency = "USD",
                ReviewedByUserId = string.Equals(status, "Requested", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : _current.UserId,
                ReviewedAtUtc = string.Equals(status, "Requested", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : now,
                CreatedAtUtc = now.AddMinutes(-10)
            };
            Db.ReturnRequests.Add(row);
            await Db.SaveChangesAsync();

            Db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
            {
                ReturnRequestId = row.Id,
                OldStatus = "None",
                NewStatus = "Requested",
                ChangedByUserId = ClientUserId,
                ChangedAtUtc = now.AddMinutes(-10),
                Note = "Created by client"
            });

            if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                Db.ReturnRequestStatusHistories.Add(new ReturnRequestStatusHistory
                {
                    ReturnRequestId = row.Id,
                    OldStatus = "Requested",
                    NewStatus = "Approved",
                    ChangedByUserId = _current.UserId,
                    ChangedAtUtc = now.AddMinutes(-5),
                    Note = "Approved by admin"
                });
            }

            await Db.SaveChangesAsync();
            return row.Id;
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
