using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DoorMarket.Tests;

public class AdminCheckoutObservabilityControllerTests
{
    [Fact]
    public async Task CheckoutAlertingService_RunOnce_CreatesThenResolvesIncidents()
    {
        await using var fixture = await CheckoutObservabilityFixture.CreateAsync();
        await fixture.SeedDegradedCheckoutWindowAsync();

        var run1 = await fixture.CreateAlertingService().RunOnceAsync(CancellationToken.None);
        Assert.True(run1.TriggeredAlerts > 0);
        Assert.True(run1.NewIncidents > 0);

        var openAfterRun1 = await fixture.Db.CheckoutAlertIncidents.AsNoTracking()
            .CountAsync(x => x.ResolvedAtUtc == null);
        Assert.True(openAfterRun1 > 0);

        fixture.Db.CheckoutAnalyticsEvents.RemoveRange(fixture.Db.CheckoutAnalyticsEvents);
        await fixture.Db.SaveChangesAsync();

        var run2 = await fixture.CreateAlertingService().RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, run2.TriggeredAlerts);
        Assert.True(run2.ResolvedIncidents > 0);

        var openAfterRun2 = await fixture.Db.CheckoutAlertIncidents.AsNoTracking()
            .CountAsync(x => x.ResolvedAtUtc == null);
        Assert.Equal(0, openAfterRun2);
    }

    [Fact]
    public async Task Incidents_AcknowledgeThenReopen_TogglesVisibility()
    {
        await using var fixture = await CheckoutObservabilityFixture.CreateAsync();
        await fixture.SeedDegradedCheckoutWindowAsync();
        await fixture.CreateAlertingService().RunOnceAsync(CancellationToken.None);

        var controller = fixture.CreateController();

        var incidentsResult = await controller.GetIncidents(
            from: null,
            to: null,
            defaultDays: 14,
            includeResolved: false,
            includeAcknowledged: true,
            take: 50,
            ct: CancellationToken.None);
        var incidentsOk = Assert.IsType<OkObjectResult>(incidentsResult.Result);
        var incidents = Assert.IsAssignableFrom<IReadOnlyList<AdminCheckoutObservabilityController.CheckoutAlertIncidentDto>>(incidentsOk.Value);
        Assert.NotEmpty(incidents);

        var target = incidents[0];
        var ackResult = await controller.AcknowledgeIncident(
            target.Id,
            new AdminCheckoutObservabilityController.IncidentAcknowledgeRequest("Investigating provider"),
            CancellationToken.None);
        var ackOk = Assert.IsType<OkObjectResult>(ackResult.Result);
        var ackDto = Assert.IsType<AdminCheckoutObservabilityController.CheckoutAlertIncidentDto>(ackOk.Value);
        Assert.True(ackDto.IsAcknowledged);

        var unresolvedOnlyResult = await controller.GetIncidents(
            from: null,
            to: null,
            defaultDays: 14,
            includeResolved: false,
            includeAcknowledged: false,
            take: 50,
            ct: CancellationToken.None);
        var unresolvedOnlyOk = Assert.IsType<OkObjectResult>(unresolvedOnlyResult.Result);
        var unresolvedOnly = Assert.IsAssignableFrom<IReadOnlyList<AdminCheckoutObservabilityController.CheckoutAlertIncidentDto>>(unresolvedOnlyOk.Value);
        Assert.DoesNotContain(unresolvedOnly, x => x.Id == target.Id);

        var reopenResult = await controller.ReopenIncident(target.Id, CancellationToken.None);
        var reopenOk = Assert.IsType<OkObjectResult>(reopenResult.Result);
        var reopenDto = Assert.IsType<AdminCheckoutObservabilityController.CheckoutAlertIncidentDto>(reopenOk.Value);
        Assert.False(reopenDto.IsAcknowledged);

        var unresolvedAfterReopenResult = await controller.GetIncidents(
            from: null,
            to: null,
            defaultDays: 14,
            includeResolved: false,
            includeAcknowledged: false,
            take: 50,
            ct: CancellationToken.None);
        var unresolvedAfterReopenOk = Assert.IsType<OkObjectResult>(unresolvedAfterReopenResult.Result);
        var unresolvedAfterReopen = Assert.IsAssignableFrom<IReadOnlyList<AdminCheckoutObservabilityController.CheckoutAlertIncidentDto>>(unresolvedAfterReopenOk.Value);
        Assert.Contains(unresolvedAfterReopen, x => x.Id == target.Id);
    }

    [Fact]
    public async Task Dashboard_ReturnsJobsAndQueueSummary()
    {
        await using var fixture = await CheckoutObservabilityFixture.CreateAsync();
        await fixture.SeedDegradedCheckoutWindowAsync();

        fixture.Db.CartRecoveryJobRuns.Add(new CartRecoveryJobRun
        {
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-40),
            EndedAtUtc = DateTime.UtcNow.AddMinutes(-39),
            DurationMs = 55_000,
            Success = true,
            CandidatesScanned = 12,
            EventsCreated = 3,
            RemindersSent = 2
        });

        fixture.Db.ReconciliationJobRuns.Add(new ReconciliationJobRun
        {
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-35),
            EndedAtUtc = DateTime.UtcNow.AddMinutes(-32),
            DurationMs = 180_000,
            Success = false,
            InProgress = false,
            Error = "timeout"
        });

        var campaign = new MarketingCampaign
        {
            Name = "OBS test campaign",
            Status = "Active"
        };
        fixture.Db.MarketingCampaigns.Add(campaign);

        fixture.Db.MarketingCampaignRuns.Add(new MarketingCampaignRun
        {
            Campaign = campaign,
            RunType = "Manual",
            Status = "Completed",
            TargetUsers = 20,
            SentCount = 18,
            FailedCount = 2,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-60),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-58)
        });

        var user = new User
        {
            Email = "obs-queue-user@door-market.test",
            PasswordHash = "hash",
            Role = UserRole.Client,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        fixture.Db.Users.Add(user);

        var cart = new Cart
        {
            User = user,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-3),
            UpdatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        fixture.Db.Carts.Add(cart);
        await fixture.Db.SaveChangesAsync();

        fixture.Db.AbandonedCartEvents.Add(new AbandonedCartEvent
        {
            UserId = user.Id,
            CartId = cart.Id,
            Currency = "USD",
            ItemCount = 1,
            Subtotal = 12,
            LastCartActivityAtUtc = DateTime.UtcNow.AddHours(-2),
            DetectedAtUtc = DateTime.UtcNow.AddMinutes(-55),
            ReminderStatus = "Pending"
        });

        fixture.Db.TransactionalNotificationLogs.AddRange(
            new TransactionalNotificationLog
            {
                NotificationType = "OrderCreated",
                Channel = "Email",
                Recipient = "client@test",
                Subject = "Order",
                Status = "Failed",
                AttemptedAtUtc = DateTime.UtcNow.AddMinutes(-20)
            },
            new TransactionalNotificationLog
            {
                NotificationType = "OrderPaid",
                Channel = "Push",
                Recipient = "client@test",
                Subject = "Paid",
                Status = "Sent",
                AttemptedAtUtc = DateTime.UtcNow.AddMinutes(-18)
            });

        await fixture.Db.SaveChangesAsync();

        var controller = fixture.CreateController();
        var result = await controller.GetDashboard(defaultHours: 48, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductionObservabilityDashboardService.ProductionDashboardDto>(ok.Value);

        Assert.True(dto.Checkout.PaymentsInitiated > 0);
        Assert.True(dto.Jobs.CartRecovery.Runs > 0);
        Assert.True(dto.Jobs.Reconciliation.Runs > 0);
        Assert.True(dto.Jobs.MarketingAutomation.Runs > 0);
        Assert.True(dto.Queue.PendingAbandonedCarts > 0);
        Assert.True(dto.Queue.FailedNotifications > 0);
    }

    [Fact]
    public async Task CheckoutAlertingService_RunOnce_WithSlackEnabled_SendsSlackNotification()
    {
        await using var fixture = await CheckoutObservabilityFixture.CreateAsync();
        await fixture.SeedDegradedCheckoutWindowAsync();

        var recordingHandler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "ok");
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(recordingHandler));

        var options = Options.Create(new CheckoutAlertingOptions
        {
            Enabled = true,
            AnalysisWindowMinutes = 120,
            NotifyCooldownMinutes = 30,
            NotifyByEmail = false,
            NotifyBySlack = true,
            SlackWebhookUrl = "https://hooks.slack.test/services/T000/B000/XX",
            MaxIncidentsPerRun = 20
        });

        var service = new CheckoutAlertingService(
            fixture.Db,
            new CheckoutObservabilityCalculator(fixture.Db),
            options,
            new SupportEmailSender(fixture.Config, NullLogger<SupportEmailSender>.Instance),
            fixture.Config,
            NullLogger<CheckoutAlertingService>.Instance,
            httpClientFactory);

        var run = await service.RunOnceAsync(CancellationToken.None);

        Assert.True(run.TriggeredAlerts > 0);
        Assert.True(run.NotifiedIncidents > 0);
        Assert.True(run.SlackSent);
        Assert.Single(recordingHandler.Requests);
        Assert.Contains("DoorMarket checkout alerts", recordingHandler.Bodies[0], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CheckoutObservabilityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IConfiguration _config;

        private CheckoutObservabilityFixture(SqliteConnection connection, DoorMarketDbContext db, IConfiguration config)
        {
            _connection = connection;
            Db = db;
            _config = config;
        }

        public DoorMarketDbContext Db { get; }
        public IConfiguration Config => _config;

        public static async Task<CheckoutObservabilityFixture> CreateAsync()
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
                    ["Support:Email"] = "support@door-market.test",
                    ["Admin:Email"] = "admin@door-market.test"
                })
                .Build();

            return new CheckoutObservabilityFixture(connection, db, config);
        }

        public async Task SeedDegradedCheckoutWindowAsync()
        {
            var now = DateTime.UtcNow;
            var sessionPrefix = Guid.NewGuid().ToString("N");

            var rows = new List<CheckoutAnalyticsEvent>();
            for (var i = 0; i < 30; i++)
            {
                var occurred = now.AddMinutes(-50 + i);
                rows.Add(new CheckoutAnalyticsEvent
                {
                    EventType = CheckoutObservabilityEvents.CheckoutSubmitClicked,
                    SessionId = $"{sessionPrefix}-submit-{i}",
                    Source = "web",
                    OccurredAtUtc = occurred
                });
                rows.Add(new CheckoutAnalyticsEvent
                {
                    EventType = CheckoutObservabilityEvents.OrderCreated,
                    SessionId = $"{sessionPrefix}-order-{i}",
                    Source = "web",
                    OccurredAtUtc = occurred
                });
                rows.Add(new CheckoutAnalyticsEvent
                {
                    EventType = CheckoutObservabilityEvents.PaymentInitiated,
                    SessionId = $"{sessionPrefix}-pay-{i}",
                    PaymentProvider = "MOBILE_MONEY",
                    Source = "web",
                    DurationMs = 8000 + i,
                    OccurredAtUtc = occurred
                });
            }

            for (var i = 0; i < 10; i++)
            {
                rows.Add(new CheckoutAnalyticsEvent
                {
                    EventType = CheckoutObservabilityEvents.PaymentConfirmed,
                    SessionId = $"{sessionPrefix}-confirmed-{i}",
                    PaymentProvider = "MOBILE_MONEY",
                    Source = "web",
                    DurationMs = 6000 + i,
                    OccurredAtUtc = now.AddMinutes(-40 + i)
                });
            }

            for (var i = 0; i < 20; i++)
            {
                rows.Add(new CheckoutAnalyticsEvent
                {
                    EventType = CheckoutObservabilityEvents.PaymentFailed,
                    SessionId = $"{sessionPrefix}-failed-{i}",
                    PaymentProvider = "MOBILE_MONEY",
                    Source = "web",
                    Success = false,
                    ErrorCode = "provider_error",
                    OccurredAtUtc = now.AddMinutes(-35 + i)
                });
            }

            Db.CheckoutAnalyticsEvents.AddRange(rows);
            await Db.SaveChangesAsync();
        }

        public CheckoutAlertingService CreateAlertingService()
        {
            var options = Options.Create(new CheckoutAlertingOptions
            {
                Enabled = true,
                RunOnStartup = true,
                RunIntervalMinutes = 5,
                AnalysisWindowMinutes = 120,
                NotifyCooldownMinutes = 30,
                NotifyByEmail = false,
                AlertEmails = "support@door-market.test",
                MaxIncidentsPerRun = 20
            });

            return new CheckoutAlertingService(
                Db,
                new CheckoutObservabilityCalculator(Db),
                options,
                new SupportEmailSender(_config, NullLogger<SupportEmailSender>.Instance),
                _config,
                NullLogger<CheckoutAlertingService>.Instance);
        }

        public AdminCheckoutObservabilityController CreateController()
            => new(
                new CheckoutObservabilityCalculator(Db),
                new ProductionObservabilityDashboardService(Db, new CheckoutObservabilityCalculator(Db)),
                Options.Create(new CheckoutAlertingOptions()),
                Db);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
            => _client;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public RecordingHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "text/plain")
            };
        }
    }
}
