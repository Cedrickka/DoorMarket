using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Tests;

public class AdminCartRecoveryControllerTests
{
    [Fact]
    public async Task GetSummary_ComputesExpectedAbAndObservabilityMetrics()
    {
        await using var fixture = await AdminCartRecoveryFixture.CreateAsync();
        var now = DateTime.UtcNow;

        var fromUtc = now.AddDays(-1);
        var toUtc = now.AddDays(1);
        var controller = fixture.CreateController();

        var result = await controller.GetSummary(fromUtc, toUtc, conversionWindowHours: 48, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminCartRecoveryController.CartRecoverySummaryDto>(ok.Value);

        Assert.Equal(2, payload.DetectedEvents);
        Assert.Equal(1, payload.RemindersSent);
        Assert.Equal(1, payload.RemindersSkipped);
        Assert.Equal(1, payload.ConvertedEvents);
        Assert.Equal(50m, payload.ConversionRatePct);
        Assert.Equal(1, payload.GroupAEvents);
        Assert.Equal(1, payload.GroupAConverted);
        Assert.Equal(100m, payload.GroupAConversionRatePct);
        Assert.Equal(1, payload.GroupBEvents);
        Assert.Equal(0, payload.GroupBConverted);
        Assert.Equal(0m, payload.GroupBConversionRatePct);
        Assert.Equal(100m, payload.UpliftPct);
        Assert.Equal(2, payload.JobRunsCount);
        Assert.Equal(1, payload.JobRunsSuccessCount);
        Assert.Equal(1, payload.JobRunsFailureCount);
        Assert.Equal(120, payload.JobLatencyP50Ms);
        Assert.Equal(420, payload.JobLatencyP95Ms);
        Assert.Equal(1, payload.SmtpFailures);
        Assert.Equal(1, payload.PushFailures);
    }

    private sealed class FakeRecoveryService : IAbandonedCartRecoveryService
    {
        public Task<AbandonedCartRecoveryRunResult> RunOnceAsync(CancellationToken ct)
            => Task.FromResult(new AbandonedCartRecoveryRunResult(0, 0, 0, 0, 0, 0));
    }

    private sealed class AdminCartRecoveryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AdminCartRecoveryFixture(SqliteConnection connection, DoorMarketDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public DoorMarketDbContext Db { get; }

        public static async Task<AdminCartRecoveryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;

            var userA = new User
            {
                Email = "cartrecovery-a@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };
            var userB = new User
            {
                Email = "cartrecovery-b@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };
            var cartA = new Cart { User = userA };
            var cartB = new Cart { User = userB };

            db.AddRange(userA, userB, cartA, cartB);
            await db.SaveChangesAsync();

            db.AbandonedCartEvents.AddRange(
                new AbandonedCartEvent
                {
                    UserId = userA.Id,
                    CartId = cartA.Id,
                    Currency = "USD",
                    ItemCount = 2,
                    Subtotal = 25m,
                    LastCartActivityAtUtc = now.AddHours(-3),
                    DetectedAtUtc = now.AddHours(-2),
                    ExperimentGroup = "A",
                    ReminderStatus = "Sent",
                    ReminderAttemptCount = 1,
                    SentChannels = "Email",
                    RecipientEmail = userA.Email,
                    ReminderSentAtUtc = now.AddHours(-2)
                },
                new AbandonedCartEvent
                {
                    UserId = userB.Id,
                    CartId = cartB.Id,
                    Currency = "USD",
                    ItemCount = 1,
                    Subtotal = 12m,
                    LastCartActivityAtUtc = now.AddHours(-3),
                    DetectedAtUtc = now.AddHours(-2),
                    ExperimentGroup = "B",
                    ReminderStatus = "Skipped",
                    ReminderAttemptCount = 0,
                    Error = "Holdout group (B): reminder skipped."
                });

            db.Orders.Add(new Order
            {
                UserId = userA.Id,
                Status = "Created",
                PaymentStatus = "Unpaid",
                FulfillmentStatus = "PendingPayment",
                PaymentProvider = "PayPal",
                Currency = "USD",
                Subtotal = 25m,
                TotalItemsAmount = 25m,
                PlatformFeeTotal = 1m,
                DeliveryFee = 3m,
                Discount = 0m,
                TotalAmount = 28m,
                DeliveryName = "Client A",
                DeliveryPhone = "+243000000001",
                DeliveryLine1 = "Address A",
                DeliveryCity = "Kinshasa",
                DeliveryCountry = "CD",
                CreatedAtUtc = now.AddHours(-1)
            });

            db.CartRecoveryJobRuns.AddRange(
                new CartRecoveryJobRun
                {
                    StartedAtUtc = now.AddMinutes(-30),
                    EndedAtUtc = now.AddMinutes(-29),
                    DurationMs = 120,
                    Success = true
                },
                new CartRecoveryJobRun
                {
                    StartedAtUtc = now.AddMinutes(-20),
                    EndedAtUtc = now.AddMinutes(-19),
                    DurationMs = 420,
                    Success = false,
                    Error = "SMTP timeout"
                });

            db.TransactionalNotificationLogs.AddRange(
                new TransactionalNotificationLog
                {
                    NotificationType = NotificationEvents.CartAbandonedReminderEmail,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = userA.Email,
                    Subject = "Cart reminder email",
                    Status = NotificationEvents.StatusFailed,
                    Error = "SMTP timeout",
                    AttemptedAtUtc = now.AddMinutes(-18)
                },
                new TransactionalNotificationLog
                {
                    NotificationType = NotificationEvents.CartAbandonedReminderPush,
                    Channel = NotificationEvents.ChannelPush,
                    Recipient = userA.Id.ToString(),
                    Subject = "Cart reminder push",
                    Status = NotificationEvents.StatusFailed,
                    Error = "FCM 500",
                    AttemptedAtUtc = now.AddMinutes(-17)
                });

            await db.SaveChangesAsync();

            return new AdminCartRecoveryFixture(connection, db);
        }

        public AdminCartRecoveryController CreateController()
            => new(Db, new FakeRecoveryService());

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
