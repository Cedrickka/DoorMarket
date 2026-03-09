using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DoorMarket.Tests;

public class AbandonedCartRecoveryServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WithStaleCart_CreatesEventAndSendsEmail()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        await fixture.SeedStaleCartAsync();

        var service = fixture.CreateService(new CartRecoveryOptions
        {
            Enabled = true,
            AbTestEnabled = false,
            StaleAfterMinutes = 60,
            AntiSpamWindowMinutes = 24 * 60,
            ScanBatchSize = 20,
            MaxNotificationsPerRun = 20,
            PushEnabled = false
        });

        var run = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, run.CandidatesScanned);
        Assert.Equal(1, run.EventsCreated);
        Assert.Equal(1, run.RemindersSent);
        Assert.Equal(0, run.RemindersFailed);
        Assert.Equal(0, run.AntiSpamSkipped);
        Assert.Equal(0, run.ConvertedSkipped);

        var evt = await fixture.Db.AbandonedCartEvents.AsNoTracking().SingleAsync();
        Assert.Equal("Sent", evt.ReminderStatus);
        Assert.Equal("Email", evt.SentChannels);
        Assert.Equal(1, evt.ReminderAttemptCount);
        Assert.NotNull(evt.ReminderSentAtUtc);

        var notif = await fixture.Db.TransactionalNotificationLogs.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationEvents.CartAbandonedReminderEmail, notif.NotificationType);
        Assert.Equal(NotificationEvents.StatusSent, notif.Status);
        Assert.Equal(NotificationEvents.ChannelEmail, notif.Channel);
        Assert.Null(notif.OrderId);
    }

    [Fact]
    public async Task RunOnceAsync_WithRecentEvent_SkipsByAntiSpam()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        await fixture.SeedStaleCartAsync();

        var service = fixture.CreateService(new CartRecoveryOptions
        {
            Enabled = true,
            AbTestEnabled = false,
            StaleAfterMinutes = 60,
            AntiSpamWindowMinutes = 24 * 60,
            ScanBatchSize = 20,
            MaxNotificationsPerRun = 20,
            PushEnabled = false
        });

        var firstRun = await service.RunOnceAsync(CancellationToken.None);
        var secondRun = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, firstRun.EventsCreated);
        Assert.Equal(0, secondRun.EventsCreated);
        Assert.Equal(1, secondRun.AntiSpamSkipped);

        var eventCount = await fixture.Db.AbandonedCartEvents.AsNoTracking().CountAsync();
        Assert.Equal(1, eventCount);

        Assert.Single(fixture.EmailSender.Messages);
    }

    [Fact]
    public async Task RunOnceAsync_WhenOrderExistsAfterCartActivity_SkipsAsConverted()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        var seeded = await fixture.SeedStaleCartAsync();

        fixture.Db.Orders.Add(new Order
        {
            UserId = seeded.UserId,
            Status = "Created",
            PaymentStatus = "Unpaid",
            FulfillmentStatus = "PendingPayment",
            PaymentProvider = "PayPal",
            Currency = "USD",
            Subtotal = 10m,
            TotalItemsAmount = 10m,
            PlatformFeeTotal = 1m,
            DeliveryFee = 2m,
            Discount = 0m,
            TotalAmount = 12m,
            DeliveryName = "Client Test",
            DeliveryPhone = "+243000000000",
            DeliveryLine1 = "Address",
            DeliveryCity = "Kinshasa",
            DeliveryCountry = "CD",
            CreatedAtUtc = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        var service = fixture.CreateService(new CartRecoveryOptions
        {
            Enabled = true,
            AbTestEnabled = false,
            StaleAfterMinutes = 60,
            AntiSpamWindowMinutes = 24 * 60,
            ScanBatchSize = 20,
            MaxNotificationsPerRun = 20,
            PushEnabled = false
        });

        var run = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, run.CandidatesScanned);
        Assert.Equal(0, run.EventsCreated);
        Assert.Equal(1, run.ConvertedSkipped);
        Assert.Equal(0, run.RemindersSent);

        Assert.Empty(fixture.EmailSender.Messages);
        Assert.Equal(0, await fixture.Db.AbandonedCartEvents.AsNoTracking().CountAsync());
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RecoveryFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            FakeEmailSender emailSender,
            IConfiguration config)
        {
            _connection = connection;
            Db = db;
            EmailSender = emailSender;
            Config = config;
        }

        public DoorMarketDbContext Db { get; }
        public FakeEmailSender EmailSender { get; }
        public IConfiguration Config { get; }

        public static async Task<RecoveryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:WebBaseUrl"] = "https://door-market.test/"
                })
                .Build();

            return new RecoveryFixture(connection, db, new FakeEmailSender(), config);
        }

        public async Task<SeededCartData> SeedStaleCartAsync()
        {
            var now = DateTime.UtcNow;

            var user = new User
            {
                Email = "cart-reminder@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var owner = new User
            {
                Email = "shop-owner@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Cart reminder category",
                Slug = "cart-reminder-category"
            };

            var shop = new Shop
            {
                OwnerUser = owner,
                Name = "Reminder Shop",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Reminder Product",
                Price = 10m,
                Currency = "USD",
                StockQty = 30,
                IsActive = true
            };

            var cart = new Cart
            {
                User = user
            };

            var cartItem = new CartItem
            {
                Cart = cart,
                Product = product,
                Qty = 2,
                UnitPrice = 10m,
                CreatedAtUtc = now.AddHours(-5),
                UpdatedAtUtc = now.AddHours(-5)
            };

            Db.AddRange(user, owner, category, shop, product, cart, cartItem);
            await Db.SaveChangesAsync();

            return new SeededCartData(user.Id, cart.Id);
        }

        public AbandonedCartRecoveryService CreateService(CartRecoveryOptions options)
            => new(
                Db,
                EmailSender,
                new FakePushSender(),
                Options.Create(options),
                Config,
                NullLogger<AbandonedCartRecoveryService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<SentEmail> Messages { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            Messages.Add(new SentEmail(toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePushSender : ICartReminderPushSender
    {
        public Task<PushSendResult> SendCartReminderAsync(Guid userId, string title, string body, CancellationToken ct)
            => Task.FromResult(new PushSendResult(Attempted: false, Sent: false, Error: null));
    }

    private sealed record SentEmail(string ToEmail, string Subject, string Body);
    private sealed record SeededCartData(Guid UserId, Guid CartId);
}
