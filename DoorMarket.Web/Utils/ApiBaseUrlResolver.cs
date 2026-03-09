using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DoorMarket.Web.Utils;

public static class ApiBaseUrlResolver
{
    public static string Resolve(IConfiguration config, IHostEnvironment env)
    {
        var baseUrl = config["Api:BaseUrl"]
            ?? config["ApiBaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            (!env.IsDevelopment()
             && baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            baseUrl = env.IsDevelopment()
                ? "https://localhost:7288/"
                : "http://api.door-market.com/";
        }

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return baseUrl;
    }
}
