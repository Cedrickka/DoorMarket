using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DoorMarket.Tests;

public class AdminNotificationsControllerTests
{
    [Fact]
    public async Task GetTransactions_WithStatusAndOrderFilter_ReturnsMatchingItems()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var result = await controller.GetTransactions(
            orderId: fixture.TargetOrderId,
            type: null,
            status: NotificationEvents.StatusFailed,
            from: null,
            to: null,
            page: 1,
            pageSize: 20,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<AdminNotificationsController.TransactionsPageDto>(ok.Value);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(NotificationEvents.ClientPaymentFailed, page.Items[0].NotificationType);
        Assert.Equal(NotificationEvents.StatusFailed, page.Items[0].Status);
    }

    [Fact]
    public async Task GetSummary_ReturnsAggregatesByType()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var result = await controller.GetSummary(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationsController.TransactionNotificationSummaryDto>>(ok.Value);

        var clientFailed = rows.First(x => x.NotificationType == NotificationEvents.ClientPaymentFailed);
        Assert.Equal(3, clientFailed.Total);
        Assert.Equal(1, clientFailed.Sent);
        Assert.Equal(2, clientFailed.Failed);
    }

    [Fact]
    public async Task RetryTransaction_WhenNotFound_ReturnsNotFound()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);
        var missingId = Guid.NewGuid();
        replay.Results[missingId] = new NotificationReplayResult(
            Found: false,
            Retried: false,
            Outcome: "NotFound",
            Message: "Notification introuvable.",
            OrderId: null,
            NotificationType: null);

        var result = await controller.RetryTransaction(missingId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task RetryTransaction_WhenFound_DelegatesToReplayService()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var targetId = fixture.UnresolvedFailedLogId;
        replay.Results[targetId] = new NotificationReplayResult(
            Found: true,
            Retried: true,
            Outcome: "Triggered",
            Message: "Replay declenche.",
            OrderId: fixture.TargetOrderId,
            NotificationType: NotificationEvents.ClientPaymentFailed);

        var result = await controller.RetryTransaction(targetId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminNotificationsController.RetryTransactionDto>(ok.Value);
        Assert.True(dto.Retried);
        Assert.Equal("Triggered", dto.Outcome);
        Assert.Contains(targetId, replay.Calls);
    }

    [Fact]
    public async Task RetryFailedTransactions_OnlyRetriesUnresolvedFailures()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var result = await controller.RetryFailedTransactions(limit: 10, type: NotificationEvents.ClientPaymentFailed, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminNotificationsController.BulkRetryTransactionsDto>(ok.Value);
        Assert.Equal(1, dto.Candidates);
        Assert.Equal(1, dto.Triggered);
        Assert.Equal(0, dto.Ignored);
        Assert.Equal(0, dto.Failed);
        Assert.Single(replay.Calls);
        Assert.Equal(fixture.UnresolvedFailedLogId, replay.Calls[0]);
    }

    [Fact]
    public async Task GetIncidents_GroupsUnresolvedFailures()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var baseLog = await fixture.Db.TransactionalNotificationLogs
            .AsNoTracking()
            .FirstAsync(x => x.Id == fixture.UnresolvedFailedLogId);

        fixture.Db.TransactionalNotificationLogs.Add(new TransactionalNotificationLog
        {
            OrderId = baseLog.OrderId,
            NotificationType = baseLog.NotificationType,
            Channel = NotificationEvents.ChannelEmail,
            Recipient = baseLog.Recipient,
            Subject = "second unresolved failure",
            Status = NotificationEvents.StatusFailed,
            Error = "smtp timeout retry",
            AttemptedAtUtc = baseLog.AttemptedAtUtc.AddMinutes(-1)
        });
        await fixture.Db.SaveChangesAsync();

        var result = await controller.GetIncidents(
            orderId: null,
            type: NotificationEvents.ClientPaymentFailed,
            from: null,
            to: null,
            minFailures: 2,
            take: 20,
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationsController.NotificationIncidentDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal(2, rows[0].FailedCount);
        Assert.Equal(baseLog.Recipient, rows[0].Recipient);
    }

    [Fact]
    public async Task AcknowledgeIncident_HidesItFromDefaultUnresolvedList()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var ackResult = await controller.AcknowledgeIncident(
            fixture.UnresolvedFailedLogId,
            new AdminNotificationsController.IncidentAcknowledgeRequest("Investigating provider issue"),
            CancellationToken.None);

        var ackOk = Assert.IsType<OkObjectResult>(ackResult.Result);
        var ackDto = Assert.IsType<AdminNotificationsController.IncidentAcknowledgeDto>(ackOk.Value);
        Assert.True(ackDto.IsActive);

        var unresolvedDefault = await controller.GetIncidents(
            orderId: null,
            type: NotificationEvents.ClientPaymentFailed,
            from: null,
            to: null,
            minFailures: 1,
            includeAcknowledged: false,
            take: 20,
            ct: CancellationToken.None);

        var unresolvedDefaultOk = Assert.IsType<OkObjectResult>(unresolvedDefault.Result);
        var unresolvedDefaultRows = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationsController.NotificationIncidentDto>>(unresolvedDefaultOk.Value);
        Assert.Empty(unresolvedDefaultRows);

        var unresolvedWithAcknowledged = await controller.GetIncidents(
            orderId: null,
            type: NotificationEvents.ClientPaymentFailed,
            from: null,
            to: null,
            minFailures: 1,
            includeAcknowledged: true,
            take: 20,
            ct: CancellationToken.None);

        var unresolvedWithAcknowledgedOk = Assert.IsType<OkObjectResult>(unresolvedWithAcknowledged.Result);
        var unresolvedWithAcknowledgedRows = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationsController.NotificationIncidentDto>>(unresolvedWithAcknowledgedOk.Value);
        Assert.Single(unresolvedWithAcknowledgedRows);
        Assert.True(unresolvedWithAcknowledgedRows[0].Acknowledged);
        Assert.Equal("Investigating provider issue", unresolvedWithAcknowledgedRows[0].AcknowledgementNote);
    }

    [Fact]
    public async Task ReopenIncident_MakesItVisibleAgainInDefaultList()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var ackResult = await controller.AcknowledgeIncident(
            fixture.UnresolvedFailedLogId,
            new AdminNotificationsController.IncidentAcknowledgeRequest(null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(ackResult.Result);

        var reopenResult = await controller.ReopenIncident(fixture.UnresolvedFailedLogId, CancellationToken.None);
        var reopenOk = Assert.IsType<OkObjectResult>(reopenResult.Result);
        var reopenDto = Assert.IsType<AdminNotificationsController.IncidentAcknowledgeDto>(reopenOk.Value);
        Assert.False(reopenDto.IsActive);

        var unresolvedDefault = await controller.GetIncidents(
            orderId: null,
            type: NotificationEvents.ClientPaymentFailed,
            from: null,
            to: null,
            minFailures: 1,
            includeAcknowledged: false,
            take: 20,
            ct: CancellationToken.None);

        var unresolvedDefaultOk = Assert.IsType<OkObjectResult>(unresolvedDefault.Result);
        var unresolvedDefaultRows = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationsController.NotificationIncidentDto>>(unresolvedDefaultOk.Value);
        Assert.Single(unresolvedDefaultRows);
        Assert.False(unresolvedDefaultRows[0].Acknowledged);
    }

    [Fact]
    public async Task ExportTransactionsCsv_ReturnsCsvPayload()
    {
        await using var fixture = await NotificationFixture.CreateAsync();
        var replay = new FakeNotificationReplayService();
        var controller = new AdminNotificationsController(fixture.Db, replay);

        var result = await controller.ExportTransactionsCsv(
            orderId: null,
            type: NotificationEvents.ClientPaymentFailed,
            status: NotificationEvents.StatusFailed,
            from: null,
            to: null,
            maxRows: 100,
            ct: CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Id,OrderId,NotificationType,Channel,Recipient,Subject,Status,Error,AttemptedAtUtc", csv, StringComparison.Ordinal);
        Assert.Contains("ClientPaymentFailed", csv, StringComparison.Ordinal);
        Assert.Contains("client1@test.local", csv, StringComparison.Ordinal);
    }

    private sealed class NotificationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private NotificationFixture(SqliteConnection connection, DoorMarketDbContext db, Guid targetOrderId, Guid unresolvedFailedLogId)
        {
            _connection = connection;
            Db = db;
            TargetOrderId = targetOrderId;
            UnresolvedFailedLogId = unresolvedFailedLogId;
        }

        public DoorMarketDbContext Db { get; }
        public Guid TargetOrderId { get; }
        public Guid UnresolvedFailedLogId { get; }

        public static async Task<NotificationFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var targetOrderId = Guid.NewGuid();
            var unresolvedFailedLogId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.TransactionalNotificationLogs.AddRange(
                new TransactionalNotificationLog
                {
                    OrderId = targetOrderId,
                    NotificationType = NotificationEvents.ClientPaymentFailed,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client1@test.local",
                    Subject = "fail 1",
                    Status = NotificationEvents.StatusFailed,
                    Error = "provider error",
                    AttemptedAtUtc = now.AddMinutes(-2)
                },
                new TransactionalNotificationLog
                {
                    OrderId = targetOrderId,
                    NotificationType = NotificationEvents.ClientPaymentFailed,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client1@test.local",
                    Subject = "fail recover",
                    Status = NotificationEvents.StatusSent,
                    AttemptedAtUtc = now.AddMinutes(-1)
                },
                new TransactionalNotificationLog
                {
                    Id = unresolvedFailedLogId,
                    OrderId = Guid.NewGuid(),
                    NotificationType = NotificationEvents.ClientPaymentFailed,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client3@test.local",
                    Subject = "fail pending replay",
                    Status = NotificationEvents.StatusFailed,
                    Error = "smtp timeout",
                    AttemptedAtUtc = now.AddMinutes(-4)
                },
                new TransactionalNotificationLog
                {
                    OrderId = Guid.NewGuid(),
                    NotificationType = NotificationEvents.ClientOrderCreated,
                    Channel = NotificationEvents.ChannelEmail,
                    Recipient = "client2@test.local",
                    Subject = "created",
                    Status = NotificationEvents.StatusSent,
                    AttemptedAtUtc = now.AddMinutes(-3)
                });

            await db.SaveChangesAsync();

            return new NotificationFixture(connection, db, targetOrderId, unresolvedFailedLogId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeNotificationReplayService : INotificationReplayService
    {
        public List<Guid> Calls { get; } = new();
        public Dictionary<Guid, NotificationReplayResult> Results { get; } = new();

        public Task<NotificationReplayResult> RetryAsync(Guid notificationLogId, CancellationToken ct)
        {
            Calls.Add(notificationLogId);
            if (Results.TryGetValue(notificationLogId, out var value))
            {
                return Task.FromResult(value);
            }

            return Task.FromResult(new NotificationReplayResult(
                Found: true,
                Retried: true,
                Outcome: "Triggered",
                Message: "Replay declenche.",
                OrderId: null,
                NotificationType: null));
        }
    }
}
