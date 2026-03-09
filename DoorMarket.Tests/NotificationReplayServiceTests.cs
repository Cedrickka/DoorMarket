using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorMarket.Tests;

public class NotificationReplayServiceTests
{
    [Fact]
    public async Task RetryAsync_WhenNotFound_ReturnsNotFound()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var svc = fixture.CreateService();

        var result = await svc.RetryAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Found);
        Assert.False(result.Retried);
        Assert.Equal("NotFound", result.Outcome);
    }

    [Fact]
    public async Task RetryAsync_WhenStatusIsNotFailed_ReturnsIgnored()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var svc = fixture.CreateService();

        var result = await svc.RetryAsync(fixture.SentLogId, CancellationToken.None);

        Assert.True(result.Found);
        Assert.False(result.Retried);
        Assert.Equal("IgnoredNotFailed", result.Outcome);
    }

    [Fact]
    public async Task RetryAsync_WhenFailedClientPaymentPaid_DelegatesToClientService()
    {
        await using var fixture = await ReplayFixture.CreateAsync();
        var svc = fixture.CreateService();

        var result = await svc.RetryAsync(fixture.FailedClientPaidLogId, CancellationToken.None);

        Assert.True(result.Found);
        Assert.True(result.Retried);
        Assert.Equal("Triggered", result.Outcome);
        Assert.Equal(1, fixture.ClientService.PaidCalls);
        Assert.Equal(0, fixture.ClientService.CreatedCalls);
        Assert.Equal(0, fixture.ClientService.FailedCalls);
    }

    private sealed class ReplayFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ReplayFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            FakeClientOrderNotificationService clientService,
            FakeOrderNotificationService shopService,
            FakeAdminOrderNotificationService adminService,
            Guid sentLogId,
            Guid failedClientPaidLogId)
        {
            _connection = connection;
            Db = db;
            ClientService = clientService;
            ShopService = shopService;
            AdminService = adminService;
            SentLogId = sentLogId;
            FailedClientPaidLogId = failedClientPaidLogId;
        }

        public DoorMarketDbContext Db { get; }
        public FakeClientOrderNotificationService ClientService { get; }
        public FakeOrderNotificationService ShopService { get; }
        public FakeAdminOrderNotificationService AdminService { get; }
        public Guid SentLogId { get; }
        public Guid FailedClientPaidLogId { get; }

        public NotificationReplayService CreateService()
            => new(
                Db,
                ClientService,
                ShopService,
                AdminService,
                NullLogger<NotificationReplayService>.Instance);

        public static async Task<ReplayFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var orderId = Guid.NewGuid();
            var sentLogId = Guid.NewGuid();
            var failedClientPaidLogId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.TransactionalNotificationLogs.AddRange(
                new TransactionalNotificationLog
                {
                    Id = sentLogId,
                    OrderId = orderId,
                    NotificationType = NotificationEvents.ClientOrderCreated,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client@test.local",
                    Subject = "created",
                    Status = NotificationEvents.StatusSent,
                    AttemptedAtUtc = now.AddMinutes(-3)
                },
                new TransactionalNotificationLog
                {
                    Id = failedClientPaidLogId,
                    OrderId = orderId,
                    NotificationType = NotificationEvents.ClientPaymentPaid,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client@test.local",
                    Subject = "paid",
                    Status = NotificationEvents.StatusFailed,
                    Error = "smtp timeout",
                    AttemptedAtUtc = now.AddMinutes(-1)
                });
            await db.SaveChangesAsync();

            return new ReplayFixture(
                connection,
                db,
                new FakeClientOrderNotificationService(),
                new FakeOrderNotificationService(),
                new FakeAdminOrderNotificationService(),
                sentLogId,
                failedClientPaidLogId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeClientOrderNotificationService : IClientOrderNotificationService
    {
        public int CreatedCalls { get; private set; }
        public int PaidCalls { get; private set; }
        public int FailedCalls { get; private set; }

        public Task NotifyClientOrderCreatedAsync(Guid orderId, CancellationToken ct)
        {
            CreatedCalls++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentPaidAsync(Guid orderId, CancellationToken ct)
        {
            PaidCalls++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct)
        {
            FailedCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderNotificationService : IOrderNotificationService
    {
        public Task NotifyShopsOrderPaidPendingOnceAsync(Guid orderId, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeAdminOrderNotificationService : IAdminOrderNotificationService
    {
        public Task NotifyAdminOrderPaidOnceAsync(Guid orderId, CancellationToken ct)
            => Task.CompletedTask;
    }
}
