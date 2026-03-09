using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Services;

public sealed class ReconciliationCronWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReconciliationCronOptions _options;
    private readonly ILogger<ReconciliationCronWorker> _logger;

    public ReconciliationCronWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReconciliationCronOptions> options,
        ILogger<ReconciliationCronWorker> logger)
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
            if (_options.RunOnStartup)
            {
                await RunOnceSafeAsync(stoppingToken);
            }

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

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IReconciliationCronService>();
            var run = await service.RunOnceAsync("Worker", null, ct);

            _logger.LogInformation(
                "Reconciliation cron run completed. runId={RunId} lock={Lock} success={Success} skipped={Skipped} scanned={Scanned} insights={Insights} overlapPairs={OverlapPairs}",
                run.RunId,
                run.AcquiredLock,
                run.Success,
                run.WasSkipped,
                run.CandidatePayouts,
                run.InsightsCount,
                run.OverlapPairCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconciliation cron run failed.");
        }
    }
}
