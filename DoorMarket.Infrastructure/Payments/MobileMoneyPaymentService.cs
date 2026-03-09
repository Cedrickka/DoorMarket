using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DoorMarket.Infrastructure.Payments;

public class MobileMoneyPaymentService : IMobileMoneyPaymentService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public MobileMoneyPaymentService(
        DoorMarketDbContext db,
        ICurrentUserService current,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _current = current;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<MobileMoneyCheckoutResult> InitiateAsync(
        Guid orderId,
        string provider,
        string phoneNumber,
        string? callbackUrl,
        CancellationToken ct)
    {
        var normalizedProvider = NormalizeProvider(provider);
        if (normalizedProvider is null)
        {
            throw new InvalidOperationException("Provider Mobile Money invalide.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new InvalidOperationException("Numero de telephone requis.");
        }

        var order = await GetMyOrderAsync(orderId, ct);
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return new MobileMoneyCheckoutResult(normalizedProvider, $"ALREADY-{orderId:N}", "Paid", null, "Commande deja payee.");
        }

        if (!TryGetProviderConfig(normalizedProvider, out var providerConfig))
        {
            var tx = $"MM-{normalizedProvider}-{Guid.NewGuid():N}";
            return new MobileMoneyCheckoutResult(
                normalizedProvider,
                tx,
                "Pending",
                null,
                "Provider non configure. Mode fallback actif.");
        }

        var client = _httpClientFactory.CreateClient(nameof(MobileMoneyPaymentService));
        client.BaseAddress = new Uri(providerConfig.BaseUrl);

        if (!string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
        }

        var payload = new
        {
            reference = order.Id.ToString(),
            amount = order.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            currency = order.Currency.ToUpperInvariant(),
            phoneNumber = phoneNumber.Trim(),
            callbackUrl,
            channel = normalizedProvider
        };

        var response = await client.PostAsJsonAsync(providerConfig.InitiatePath, payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Initiation Mobile Money echouee ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var transactionId = ReadString(root, "transactionId") ?? ReadString(root, "id") ?? $"MM-{normalizedProvider}-{Guid.NewGuid():N}";
        var status = ReadString(root, "status") ?? "Pending";
        var checkoutUrl = ReadString(root, "checkoutUrl") ?? ReadString(root, "paymentUrl");
        var message = ReadString(root, "message");

        order.PaymentProvider = $"MobileMoney:{normalizedProvider}";
        if (string.Equals(order.PaymentStatus, "Unpaid", StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentStatus = "Pending";
        }

        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new MobileMoneyCheckoutResult(normalizedProvider, transactionId, status, checkoutUrl, message);
    }

    public async Task<MobileMoneyPaymentStatusResult> CheckStatusAsync(
        Guid orderId,
        string provider,
        string transactionId,
        CancellationToken ct)
    {
        var normalizedProvider = NormalizeProvider(provider);
        if (normalizedProvider is null)
        {
            throw new InvalidOperationException("Provider Mobile Money invalide.");
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new InvalidOperationException("transactionId requis.");
        }

        var order = await GetMyOrderAsync(orderId, ct);
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return new MobileMoneyPaymentStatusResult(normalizedProvider, transactionId, "Paid", true, "Commande deja payee.", "Paid");
        }

        if (!TryGetProviderConfig(normalizedProvider, out var providerConfig))
        {
            return new MobileMoneyPaymentStatusResult(normalizedProvider, transactionId, "Pending", false, "Provider non configure.", "Pending");
        }

        var client = _httpClientFactory.CreateClient(nameof(MobileMoneyPaymentService));
        client.BaseAddress = new Uri(providerConfig.BaseUrl);
        if (!string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
        }

        var statusPath = providerConfig.StatusPath.Replace("{transactionId}", Uri.EscapeDataString(transactionId), StringComparison.OrdinalIgnoreCase);
        var response = await client.GetAsync(statusPath, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Verification Mobile Money echouee ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var rawStatus = ReadString(root, "status") ?? ReadString(root, "paymentStatus") ?? "Pending";
        var status = NormalizePaymentStatus(rawStatus);
        var paid = status == "Paid";
        var message = ReadString(root, "message");

        return new MobileMoneyPaymentStatusResult(normalizedProvider, transactionId, status, paid, message, rawStatus);
    }

    private async Task<Domain.Entities.Order> GetMyOrderAsync(Guid orderId, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var order = await _db.Orders.AsTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        return order ?? throw new InvalidOperationException("Commande introuvable.");
    }

    private static string? NormalizeProvider(string? provider)
        => provider?.Trim().ToUpperInvariant() switch
        {
            "AIRTEL" or "AIRTEL_MONEY" => "AIRTEL",
            "ORANGE" or "ORANGE_MONEY" => "ORANGE",
            "MPESA" or "M-PESA" => "MPESA",
            _ => null
        };

    private static string NormalizePaymentStatus(string? raw)
    {
        var normalized = raw?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PAID" or "SUCCESS" or "COMPLETED" => "Paid",
            "FAILED" or "ERROR" => "Failed",
            _ => "Pending"
        };
    }

    private bool TryGetProviderConfig(string provider, out ProviderConfig config)
    {
        var section = _config.GetSection($"MobileMoney:Providers:{provider}");
        var baseUrl = section["BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            config = default;
            return false;
        }

        config = new ProviderConfig(
            BaseUrl: baseUrl.Trim(),
            ApiKey: section["ApiKey"],
            InitiatePath: section["InitiatePath"] ?? "/payments/initiate",
            StatusPath: section["StatusPath"] ?? "/payments/{transactionId}/status");
        return true;
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) ? value.GetString() : null;

    private readonly record struct ProviderConfig(
        string BaseUrl,
        string? ApiKey,
        string InitiatePath,
        string StatusPath);
}
