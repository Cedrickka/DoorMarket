using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DoorMarket.Api.HealthChecks;

public static class HealthChecksResponseWriter
{
    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            generatedAtUtc = DateTime.UtcNow,
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                error = entry.Value.Exception?.Message,
                data = entry.Value.Data.Count == 0
                    ? null
                    : entry.Value.Data.ToDictionary(x => x.Key, x => x.Value)
            })
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
