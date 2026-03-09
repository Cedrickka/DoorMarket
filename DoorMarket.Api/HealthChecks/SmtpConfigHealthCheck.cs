using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DoorMarket.Api.HealthChecks;

public sealed class SmtpConfigHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public SmtpConfigHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        AddMissing("Smtp:Host", missing);
        AddMissing("Smtp:FromEmail", missing);
        AddMissing("Smtp:Username", missing);
        AddMissing("Smtp:Password", missing);

        if (missing.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("SMTP configured."));
        }

        var data = new Dictionary<string, object>
        {
            ["missingKeys"] = missing
        };
        return Task.FromResult(HealthCheckResult.Unhealthy("SMTP configuration missing.", data: data));
    }

    private void AddMissing(string key, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(_configuration[key]))
        {
            missing.Add(key);
        }
    }
}
