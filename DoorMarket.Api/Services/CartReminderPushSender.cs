using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DoorMarket.Api.Services;

public interface ICartReminderPushSender
{
    Task<PushSendResult> SendCartReminderAsync(
        Guid userId,
        string title,
        string body,
        CancellationToken ct);
}

public sealed record PushSendResult(bool Attempted, bool Sent, string? Error);

public interface IPushProvider
{
    Task<PushProviderSendResult> SendAsync(
        string token,
        string title,
        string body,
        CancellationToken ct);
}

public sealed record PushProviderSendResult(
    bool Success,
    bool PermanentFailure,
    string? Error);

public sealed class CartReminderPushSender : ICartReminderPushSender
{
    private readonly DoorMarketDbContext _db;
    private readonly CartRecoveryOptions _options;
    private readonly IPushProvider _fcmProvider;
    private readonly IPushProvider _apnsProvider;
    private readonly ILogger<CartReminderPushSender> _logger;

    public CartReminderPushSender(
        DoorMarketDbContext db,
        IOptions<CartRecoveryOptions> options,
        FcmPushProvider fcmProvider,
        ApnsPushProvider apnsProvider,
        ILogger<CartReminderPushSender> logger)
    {
        _db = db;
        _options = options.Value;
        _fcmProvider = fcmProvider;
        _apnsProvider = apnsProvider;
        _logger = logger;
    }

    public async Task<PushSendResult> SendCartReminderAsync(
        Guid userId,
        string title,
        string body,
        CancellationToken ct)
    {
        if (!_options.PushEnabled)
        {
            return new PushSendResult(false, false, null);
        }

        var devices = await _db.UserPushDevices
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(8)
            .ToListAsync(ct);

        if (devices.Count == 0)
        {
            return new PushSendResult(true, false, "No active push devices.");
        }

        var sent = 0;
        var errors = new List<string>();

        foreach (var device in devices)
        {
            var provider = ResolveProvider(device.Platform);
            if (provider is null)
            {
                errors.Add($"Unsupported platform: {device.Platform}");
                continue;
            }

            var sendResult = await SendWithRetryAsync(provider, device.Token, title, body, ct);
            if (sendResult.Success)
            {
                sent++;
                device.LastSeenAtUtc = DateTime.UtcNow;
                device.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var error = sendResult.Error ?? "Push send failed.";
                errors.Add($"{device.Platform}:{Short(error)}");
                if (sendResult.PermanentFailure)
                {
                    device.IsActive = false;
                    device.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct);
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "Cart reminder push sent. userId={UserId} sent={Sent} total={Total}",
                userId,
                sent,
                devices.Count);
        }
        else if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Cart reminder push failed. userId={UserId} errors={Errors}",
                userId,
                string.Join(" | ", errors));
        }

        return new PushSendResult(
            Attempted: true,
            Sent: sent > 0,
            Error: errors.Count == 0 ? null : string.Join(" | ", errors));
    }

    private async Task<PushProviderSendResult> SendWithRetryAsync(
        IPushProvider provider,
        string token,
        string title,
        string body,
        CancellationToken ct)
    {
        var attempts = 0;
        PushProviderSendResult? last = null;
        while (attempts < 2)
        {
            attempts++;
            last = await provider.SendAsync(token, title, body, ct);
            if (last.Success || last.PermanentFailure)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        }

        return last ?? new PushProviderSendResult(false, false, "Push provider unavailable.");
    }

    private IPushProvider? ResolveProvider(string platform)
    {
        var normalized = (platform ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "fcm" => _fcmProvider,
            "android" => _fcmProvider,
            "apns" => _apnsProvider,
            "ios" => _apnsProvider,
            _ => null
        };
    }

    private static string Short(string text)
        => text.Length <= 180 ? text : text[..180];
}

public sealed class FcmPushProvider : IPushProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public FcmPushProvider(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    public async Task<PushProviderSendResult> SendAsync(
        string token,
        string title,
        string body,
        CancellationToken ct)
    {
        var serverKey = (_config["Push:Fcm:ServerKey"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            return new PushProviderSendResult(false, true, "FCM server key missing.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new PushProviderSendResult(false, true, "FCM token missing.");
        }

        var client = _httpFactory.CreateClient("PushProvider");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/fcm/send");
        request.Headers.TryAddWithoutValidation("Authorization", $"key={serverKey}");
        request.Content = JsonContent.Create(new
        {
            to = token,
            priority = "high",
            notification = new
            {
                title,
                body
            },
            data = new
            {
                type = "cart_recovery",
                deepLink = "/checkout"
            }
        });

        try
        {
            using var response = await client.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                if (raw.Contains("\"failure\":1", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("NotRegistered", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("InvalidRegistration", StringComparison.OrdinalIgnoreCase))
                {
                    return new PushProviderSendResult(false, true, raw);
                }

                return new PushProviderSendResult(true, false, null);
            }

            var permanent = (int)response.StatusCode is >= 400 and < 500;
            return new PushProviderSendResult(false, permanent, raw);
        }
        catch (Exception ex)
        {
            return new PushProviderSendResult(false, false, ex.Message);
        }
    }
}

public sealed class ApnsPushProvider : IPushProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public ApnsPushProvider(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    public async Task<PushProviderSendResult> SendAsync(
        string token,
        string title,
        string body,
        CancellationToken ct)
    {
        var endpoint = (_config["Push:Apns:Endpoint"] ?? "https://api.push.apple.com").Trim().TrimEnd('/');
        var bearerToken = (_config["Push:Apns:BearerToken"] ?? string.Empty).Trim();
        var topic = (_config["Push:Apns:Topic"] ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(topic))
        {
            return new PushProviderSendResult(false, true, "APNS credentials missing.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new PushProviderSendResult(false, true, "APNS token missing.");
        }

        var client = _httpFactory.CreateClient("PushProvider");
        var url = $"{endpoint}/3/device/{token}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearerToken);
        request.Headers.TryAddWithoutValidation("apns-topic", topic);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                aps = new
                {
                    alert = new
                    {
                        title,
                        body
                    },
                    sound = "default"
                },
                deepLink = "/checkout",
                type = "cart_recovery"
            }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await client.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                return new PushProviderSendResult(true, false, null);
            }

            var permanent = (int)response.StatusCode is >= 400 and < 500;
            return new PushProviderSendResult(false, permanent, raw);
        }
        catch (Exception ex)
        {
            return new PushProviderSendResult(false, false, ex.Message);
        }
    }
}
