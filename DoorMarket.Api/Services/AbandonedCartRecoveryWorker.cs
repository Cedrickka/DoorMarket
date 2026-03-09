using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Services;

public sealed class AbandonedCartRecoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CartRecoveryOptions _options;
    private readonly ILogger<AbandonedCartRecoveryWorker> _logger;

    public AbandonedCartRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<CartRecoveryOptions> options,
        ILogger<AbandonedCartRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Clamp(_options.RunIntervalMinutes, 1, 24 * 60);
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            await RunOnceSafeAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceSafeAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task RunOnceSafeAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var startedAtUtc = DateTime.UtcNow;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IAbandonedCartRecoveryService>();
            var run = await svc.RunOnceAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<DoorMarket.Infrastructure.Persistence.DoorMarketDbContext>();
            var endedAtUtc = DateTime.UtcNow;
            db.CartRecoveryJobRuns.Add(new DoorMarket.Domain.Entities.CartRecoveryJobRun
            {
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                DurationMs = (int)Math.Max(0, (endedAtUtc - startedAtUtc).TotalMilliseconds),
                Success = true,
                CandidatesScanned = run.CandidatesScanned,
                EventsCreated = run.EventsCreated,
                RemindersSent = run.RemindersSent,
                RemindersFailed = run.RemindersFailed,
                AntiSpamSkipped = run.AntiSpamSkipped,
                ConvertedSkipped = run.ConvertedSkipped
            });
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Abandoned cart recovery run completed. scanned={Scanned} events={Events} sent={Sent} failed={Failed} antiSpam={AntiSpam} converted={Converted}",
                run.CandidatesScanned,
                run.EventsCreated,
                run.RemindersSent,
                run.RemindersFailed,
                run.AntiSpamSkipped,
                run.ConvertedSkipped);
        }
        catch (Exception ex)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DoorMarket.Infrastructure.Persistence.DoorMarketDbContext>();
                var endedAtUtc = DateTime.UtcNow;
                db.CartRecoveryJobRuns.Add(new DoorMarket.Domain.Entities.CartRecoveryJobRun
                {
                    StartedAtUtc = startedAtUtc,
                    EndedAtUtc = endedAtUtc,
                    DurationMs = (int)Math.Max(0, (endedAtUtc - startedAtUtc).TotalMilliseconds),
                    Success = false,
                    CandidatesScanned = 0,
                    EventsCreated = 0,
                    RemindersSent = 0,
                    RemindersFailed = 0,
                    AntiSpamSkipped = 0,
                    ConvertedSkipped = 0,
                    Error = ex.Message
                });
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // swallow secondary persistence failure
            }
            _logger.LogError(ex, "Abandoned cart recovery run failed.");
        }
    }
}
