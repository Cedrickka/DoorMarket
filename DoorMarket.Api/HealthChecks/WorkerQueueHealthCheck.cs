using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DoorMarket.Api.HealthChecks;

public sealed class WorkerQueueHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public WorkerQueueHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabledWorkers = new List<string>();
        if (_configuration.GetValue<bool>("CartRecovery:Enabled"))
        {
            enabledWorkers.Add("CartRecovery");
        }

        if (_configuration.GetValue<bool>("MarketingAutomation:Enabled"))
        {
            enabledWorkers.Add("MarketingAutomation");
        }

        if (_configuration.GetValue<bool>("CheckoutAlerting:Enabled"))
        {
            enabledWorkers.Add("CheckoutAlerting");
        }

        if (_configuration.GetValue<bool>("ReconciliationCron:Enabled"))
        {
            enabledWorkers.Add("ReconciliationCron");
        }

        var data = new Dictionary<string, object>
        {
            ["mode"] = "in_process",
            ["enabledWorkers"] = enabledWorkers,
            ["enabledCount"] = enabledWorkers.Count
        };

        return Task.FromResult(HealthCheckResult.Healthy("Background worker queue ready.", data));
    }
}
