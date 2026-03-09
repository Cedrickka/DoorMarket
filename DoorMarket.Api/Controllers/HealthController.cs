using DoorMarket.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _current;
    private readonly IHostEnvironment _env;

    public HealthController(IConfiguration config, ICurrentUserService current, IHostEnvironment env)
    {
        _config = config;
        _current = current;
        _env = env;
    }

    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult ConfigHealth()
    {
        if (!IsAuthorizedForConfigHealth())
        {
            return Unauthorized();
        }

        var result = new
        {
            EnvironmentName = _env.EnvironmentName,
            StripeConfigured = IsStripeConfigured(),
            PayPalConfigured = IsPayPalConfigured(),
            SmtpConfigured = IsSmtpConfigured()
        };

        return Ok(result);
    }

    private bool IsAuthorizedForConfigHealth()
    {
        if (_current.IsAuthenticated &&
            (string.Equals(_current.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(_current.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var configuredToken = _config["HealthConfig:AdminToken"];
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return false;
        }

        var incomingToken = Request.Headers["X-Admin-Health-Token"].ToString();
        if (!string.Equals(incomingToken, configuredToken, StringComparison.Ordinal))
        {
            return false;
        }

        var allowedIps = (_config["HealthConfig:AllowedIps"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (allowedIps.Length == 0)
        {
            return true;
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        return !string.IsNullOrWhiteSpace(remoteIp) &&
               allowedIps.Contains(remoteIp, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsStripeConfigured()
        => !string.IsNullOrWhiteSpace(_config["Stripe:SecretKey"]) &&
           !string.IsNullOrWhiteSpace(_config["Stripe:WebhookSecret"]) &&
           !string.IsNullOrWhiteSpace(_config["Stripe:CheckoutSuccessUrl"]) &&
           !string.IsNullOrWhiteSpace(_config["Stripe:CheckoutCancelUrl"]);

    private bool IsPayPalConfigured()
        => !string.IsNullOrWhiteSpace(_config["PayPal:ClientId"]) &&
           !string.IsNullOrWhiteSpace(_config["PayPal:ClientSecret"]) &&
           !string.IsNullOrWhiteSpace(_config["PayPal:Environment"]);

    private bool IsSmtpConfigured()
        => !string.IsNullOrWhiteSpace(_config["Smtp:Host"]) &&
           !string.IsNullOrWhiteSpace(_config["Smtp:Username"]) &&
           !string.IsNullOrWhiteSpace(_config["Smtp:Password"]) &&
           !string.IsNullOrWhiteSpace(_config["Smtp:FromEmail"]);
}
