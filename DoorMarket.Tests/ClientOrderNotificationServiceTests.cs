using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorMarket.Tests;

public class ClientOrderNotificationServiceTests
{
    [Fact]
    public async Task NotifyClientOrderCreatedAsync_SendsEmail()
    {
        await using var fixture = await ClientNotificationFixture.CreateAsync("Unpaid");
        var svc = fixture.CreateService();

        await svc.NotifyClientOrderCreatedAsync(fixture.OrderId, CancellationToken.None);

        Assert.Single(fixture.EmailSender.Messages);
        Assert.Contains("Commande recue", fixture.EmailSender.Messages[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotifyClientPaymentPaidAsync_SendsOnlyWhenOrderIsPaid()
    {
        await using var fixture = await ClientNotificationFixture.CreateAsync("Pending");
        var svc = fixture.CreateService();

        await svc.NotifyClientPaymentPaidAsync(fixture.OrderId, CancellationToken.None);
        Assert.Empty(fixture.EmailSender.Messages);

        var order = await fixture.Db.Orders.FirstAsync(x => x.Id == fixture.OrderId);
        order.PaymentStatus = "Paid";
        order.PaidAtUtc = DateTime.UtcNow;
        await fixture.Db.SaveChangesAsync();

        await svc.NotifyClientPaymentPaidAsync(fixture.OrderId, CancellationToken.None);

        Assert.Single(fixture.EmailSender.Messages);
        Assert.Contains("Paiement confirme", fixture.EmailSender.Messages[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotifyClientPaymentFailedAsync_IncludesReason()
    {
        await using var fixture = await ClientNotificationFixture.CreateAsync("Failed");
        var svc = fixture.CreateService();

        await svc.NotifyClientPaymentFailedAsync(fixture.OrderId, "Capture refusee", CancellationToken.None);

        Assert.Single(fixture.EmailSender.Messages);
        Assert.Contains("Paiement echoue", fixture.EmailSender.Messages[0].Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capture refusee", fixture.EmailSender.Messages[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ClientNotificationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ClientNotificationFixture(SqliteConnection connection, DoorMarketDbContext db, Guid orderId, FakeEmailSender emailSender, IConfiguration config)
        {
            _connection = connection;
            Db = db;
            OrderId = orderId;
            EmailSender = emailSender;
            Config = config;
        }

        public DoorMarketDbContext Db { get; }
        public Guid OrderId { get; }
        public FakeEmailSender EmailSender { get; }
        public IConfiguration Config { get; }

        public ClientOrderNotificationService CreateService()
            => new(Db, EmailSender, NullLogger<ClientOrderNotificationService>.Instance, Config);

        public static async Task<ClientNotificationFixture> CreateAsync(string paymentStatus)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Email = "client-notif@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var order = new Order
            {
                User = user,
                Status = "Created",
                PaymentStatus = paymentStatus,
                FulfillmentStatus = "PendingPayment",
                PaymentProvider = "Stripe",
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
                PaidAtUtc = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null
            };

            db.Users.Add(user);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:WebBaseUrl"] = "https://door-market.test/"
                })
                .Build();

            return new ClientNotificationFixture(connection, db, order.Id, new FakeEmailSender(), config);
        }

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

    private sealed record SentEmail(string ToEmail, string Subject, string Body);
}
