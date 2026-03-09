using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Services;

public sealed class CheckoutAlertingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CheckoutAlertingOptions _options;
    private readonly ILogger<CheckoutAlertingWorker> _logger;

    public CheckoutAlertingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<CheckoutAlertingOptions> options,
        ILogger<CheckoutAlertingWorker> logger)
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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<CheckoutAlertingService>();
            var run = await service.RunOnceAsync(ct);

            _logger.LogInformation(
                "Checkout alerting run completed. triggered={Triggered} open={Open} new={New} resolved={Resolved} notified={Notified} notifyFailed={NotifyFailed}",
                run.TriggeredAlerts,
                run.OpenIncidents,
                run.NewIncidents,
                run.ResolvedIncidents,
                run.NotifiedIncidents,
                run.NotificationFailures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout alerting run failed.");
        }
    }
}
