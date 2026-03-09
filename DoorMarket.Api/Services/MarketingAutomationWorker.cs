using DoorMarket.Api.Controllers;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DoorMarket.Api.Services;

public sealed class MarketingAutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MarketingAutomationOptions> _optionsMonitor;
    private readonly ILogger<MarketingAutomationWorker> _logger;

    public MarketingAutomationWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MarketingAutomationOptions> optionsMonitor,
        ILogger<MarketingAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (_optionsMonitor.CurrentValue.RunOnStartup)
            {
                await RunOnceSafeAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var interval = Math.Clamp(_optionsMonitor.CurrentValue.RunIntervalMinutes, 1, 24 * 60);
                await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
                await RunOnceSafeAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    private async Task RunOnceSafeAsync(CancellationToken ct)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        var startedAtUtc = DateTime.UtcNow;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DoorMarketDbContext>();
            var controller = new AdminMarketingController(db);

            AdminMarketingController.MarketingAutomationResultDto? bannerResult = null;
            AdminMarketingController.MarketingCampaignBatchAutomationResultDto? campaignResult = null;
            AdminMarketingController.MarketingScenarioAutomationResultDto? scenarioResult = null;

            if (options.BannerAutomationEnabled)
            {
                var bannerAction = await controller.RunAutomation(
                    new AdminMarketingController.RunMarketingAutomationRequest(
                        options.BannerDisableExpired,
                        options.BannerAutoPrioritizeLiveByCtr,
                        options.BannerMinImpressionsForCtr),
                    ct);
                bannerResult = ExtractPayload(bannerAction);
            }

            if (options.CampaignAutomationEnabled)
            {
                var campaignAction = await controller.RunCampaignAutomation(
                    new AdminMarketingController.RunCampaignBatchAutomationRequest(
                        DryRun: false,
                        IncludeDraft: options.IncludeDraftCampaigns,
                        AutoCompleteExpired: true,
                        MaxCampaigns: options.CampaignMaxPerRun,
                        AttributionWindowDays: options.AttributionWindowDays,
                        RunType: "Scheduled"),
                    ct);
                campaignResult = ExtractPayload(campaignAction);
            }

            if (options.ScenarioAutomationEnabled)
            {
                var scenarioAction = await controller.RunScenarioAutomation(
                    new AdminMarketingController.RunMarketingScenarioAutomationRequest(
                        DryRun: false,
                        IncludeWelcome: options.IncludeWelcome,
                        IncludeWinback: options.IncludeWinback,
                        IncludeChurnRisk: options.IncludeChurnRisk,
                        MaxScenarios: options.ScenarioMaxPerRun,
                        AttributionWindowDays: options.AttributionWindowDays,
                        AntiSpamStep1Hours: options.AntiSpamStep1Hours,
                        AntiSpamStep2Hours: options.AntiSpamStep2Hours,
                        AntiSpamStep3Hours: options.AntiSpamStep3Hours,
                        ScenarioWindowDays: options.ScenarioWindowDays,
                        ScenarioMaxTouchesPerUser: options.ScenarioMaxTouchesPerUser,
                        SplitChannelsToCampaigns: options.ScenarioSplitChannelsToCampaigns),
                    ct);
                scenarioResult = ExtractPayload(scenarioAction);
            }

            var endedAtUtc = DateTime.UtcNow;
            _logger.LogInformation(
                "Marketing automation run completed in {DurationMs}ms. banners={Banners} campaignsProcessed={CampaignsProcessed} scenarioRuns={ScenarioRuns} scenarioAntiSpamSkipped={ScenarioAntiSpamSkipped}",
                Math.Max(0, (endedAtUtc - startedAtUtc).TotalMilliseconds),
                bannerResult is null ? 0 : bannerResult.DisabledExpired + bannerResult.ReprioritizedLive,
                campaignResult?.ProcessedCampaigns ?? 0,
                scenarioResult?.ExecutedRuns ?? 0,
                scenarioResult?.AntiSpamSkippedUsers ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Marketing automation worker run failed.");
        }
    }

    private static T? ExtractPayload<T>(ActionResult<T> action)
    {
        if (action.Result is OkObjectResult ok && ok.Value is T value)
        {
            return value;
        }

        return action.Value;
    }
}
