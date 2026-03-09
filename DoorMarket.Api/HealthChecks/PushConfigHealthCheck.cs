using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DoorMarket.Api.HealthChecks;

public sealed class PushConfigHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public PushConfigHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pushEnabled = _configuration.GetValue<bool>("CartRecovery:PushEnabled");
        if (!pushEnabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Push disabled by configuration."));
        }

        var hasFcm = !string.IsNullOrWhiteSpace(_configuration["Push:Fcm:ServerKey"]);
        var hasApnsToken = !string.IsNullOrWhiteSpace(_configuration["Push:Apns:BearerToken"]);
        var hasApnsTopic = !string.IsNullOrWhiteSpace(_configuration["Push:Apns:Topic"]);
        var hasApns = hasApnsToken && hasApnsTopic;

        if (hasFcm || hasApns)
        {
            var data = new Dictionary<string, object>
            {
                ["fcmConfigured"] = hasFcm,
                ["apnsConfigured"] = hasApns
            };
            return Task.FromResult(HealthCheckResult.Healthy("Push configured.", data));
        }

        return Task.FromResult(HealthCheckResult.Degraded("Push enabled but no FCM/APNS configuration found."));
    }
}
