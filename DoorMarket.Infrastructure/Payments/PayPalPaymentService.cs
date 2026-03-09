using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DoorMarket.Infrastructure.Payments;

public class PayPalPaymentService : IPayPalPaymentService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public PayPalPaymentService(
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

    public async Task<(string ApprovalUrl, string PayPalOrderId)> CreateOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await GetMyOrderAsync(orderId, ct);
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Commande deja payee.");
        }

        var token = await GetAccessTokenAsync(ct);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var returnUrlTemplate = _config["PayPal:CheckoutSuccessUrl"] ?? _config["Stripe:CheckoutSuccessUrl"];
        var cancelUrlTemplate = _config["PayPal:CheckoutCancelUrl"] ?? _config["Stripe:CheckoutCancelUrl"];
        if (string.IsNullOrWhiteSpace(returnUrlTemplate) || string.IsNullOrWhiteSpace(cancelUrlTemplate))
        {
            throw new InvalidOperationException("URLs PayPal de retour manquantes.");
        }

        var returnUrl = returnUrlTemplate.Replace("{ORDER_ID}", order.Id.ToString(), StringComparison.OrdinalIgnoreCase);
        var cancelUrl = cancelUrlTemplate.Replace("{ORDER_ID}", order.Id.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!returnUrl.Contains("orderId=", StringComparison.OrdinalIgnoreCase))
        {
            returnUrl += (returnUrl.Contains("?") ? "&" : "?") + $"orderId={order.Id}";
        }

        if (!cancelUrl.Contains("orderId=", StringComparison.OrdinalIgnoreCase))
        {
            cancelUrl += (cancelUrl.Contains("?") ? "&" : "?") + $"orderId={order.Id}";
        }

        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.Id.ToString(),
                    amount = new
                    {
                        currency_code = order.Currency.ToUpperInvariant(),
                        value = order.TotalAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                return_url = returnUrl,
                cancel_url = cancelUrl
            }
        };

        var response = await client.PostAsJsonAsync("/v2/checkout/orders", body, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayPal create-order echoue ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var payPalOrderId = root.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(payPalOrderId))
        {
            throw new InvalidOperationException("PayPal order id manquant.");
        }

        string? approvalUrl = null;
        if (root.TryGetProperty("links", out var links))
        {
            foreach (var link in links.EnumerateArray())
            {
                var rel = link.GetProperty("rel").GetString();
                if (string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase))
                {
                    approvalUrl = link.GetProperty("href").GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(approvalUrl))
        {
            throw new InvalidOperationException("URL d'approbation PayPal manquante.");
        }

        order.PaymentProvider = "PayPal";
        if (string.Equals(order.PaymentStatus, "Unpaid", StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentStatus = "Pending";
        }

        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (approvalUrl, payPalOrderId);
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(Guid orderId, string payPalOrderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payPalOrderId))
        {
            throw new InvalidOperationException("payPalOrderId manquant.");
        }

        var order = await GetMyOrderAsync(orderId, ct);
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return new PayPalCaptureResult(true, order.PaymentStatus, "Commande deja payee.", null, null);
        }

        var token = await GetAccessTokenAsync(ct);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/v2/checkout/orders/{payPalOrderId}/capture", new StringContent("{}", Encoding.UTF8, "application/json"), ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayPal capture echoue ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : null;
        var completed = string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

        string? captureId = null;
        string? funding = null;
        if (root.TryGetProperty("purchase_units", out var units) && units.GetArrayLength() > 0)
        {
            var unit = units[0];
            if (unit.TryGetProperty("payments", out var payments) &&
                payments.TryGetProperty("captures", out var captures) &&
                captures.GetArrayLength() > 0)
            {
                var capture = captures[0];
                captureId = capture.TryGetProperty("id", out var cid) ? cid.GetString() : null;
            }
        }

        if (root.TryGetProperty("payment_source", out var sourceNode))
        {
            if (sourceNode.TryGetProperty("paypal", out _))
            {
                funding = "paypal";
            }
            else if (sourceNode.TryGetProperty("card", out var card))
            {
                funding = "card";
                if (card.TryGetProperty("brand", out var brandNode))
                {
                    funding = $"{funding}:{brandNode.GetString()}";
                }
            }
        }

        if (!completed)
        {
            return new PayPalCaptureResult(false, "Pending", "Paiement PayPal en attente.", captureId, funding);
        }

        return new PayPalCaptureResult(true, "Paid", "Paiement PayPal confirme.", captureId, funding);
    }

    private async Task<Domain.Entities.Order> GetMyOrderAsync(Guid orderId, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var order = await _db.Orders.AsTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        return order ?? throw new InvalidOperationException("Commande introuvable.");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(PayPalPaymentService));
        client.BaseAddress = new Uri(GetBaseUrl());
        return client;
    }

    private string GetBaseUrl()
    {
        var env = (_config["PayPal:Environment"] ?? "sandbox").Trim().ToLowerInvariant();
        return env == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var clientId = _config["PayPal:ClientId"];
        var secret = _config["PayPal:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("PayPal non configure.");
        }

        var client = CreateClient();
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayPal auth echouee ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(payload);
        var token = doc.RootElement.TryGetProperty("access_token", out var tokenNode)
            ? tokenNode.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("PayPal access token manquant.");
        }

        return token;
    }
}
