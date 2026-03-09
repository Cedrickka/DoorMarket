using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DoorMarket.Application.DTOs.Cart;

namespace DoorMarket.Web.Services;

public sealed class CartApiClient
{
    private readonly HttpClient _http;

    public CartApiClient(HttpClient http) => _http = http;

    public async Task<CartDto> GetAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<CartDto>("api/cart", ct)
           ?? throw new InvalidOperationException("Reponse panier invalide.");

    public async Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/cart/items", new AddCartItemRequest(productId, qty), ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CartDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse panier invalide.");
    }

    public async Task<CartDto> UpdateQtyAsync(Guid itemId, int qty, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"api/cart/items/{itemId}", new UpdateCartItemRequest(qty), ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CartDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse panier invalide.");
    }

    public async Task<CartDto> RemoveItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"api/cart/items/{itemId}", ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CartDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse panier invalide.");
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync("api/cart/clear", ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
    }

    public async Task<PromoQuoteDto> ApplyPromoAsync(string code, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/cart/apply-promo", new ApplyPromoRequest(code), ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<PromoQuoteDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse promo invalide.");
    }

    public async Task<CouponValidationDto> ValidateCouponAsync(CouponValidationRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/cart/validate-coupon", req, ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CouponValidationDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse validation coupon invalide.");
    }

    public async Task<CheckoutReadinessDto> PreCheckoutAsync(PreCheckoutRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("api/cart/pre-checkout", req, ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CheckoutReadinessDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse pre-checkout invalide.");
    }

    public async Task<CartRecoveryStatusDto> GetRecoveryStatusAsync(int lookbackDays = 14, CancellationToken ct = default)
    {
        var res = await _http.GetAsync($"api/cart/recovery-status?lookbackDays={lookbackDays}", ct);
        if (!res.IsSuccessStatusCode) throw await BuildApiError(res);
        return (await res.Content.ReadFromJsonAsync<CartRecoveryStatusDto>(cancellationToken: ct))
               ?? throw new InvalidOperationException("Reponse cart recovery invalide.");
    }

    private static async Task<Exception> BuildApiError(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        var apiMessage = ExtractApiMessage(body);

        return res.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new InvalidOperationException("Non autorise (401). Veuillez vous reconnecter."),
            HttpStatusCode.Forbidden => new InvalidOperationException("Acces refuse (403)."),
            HttpStatusCode.NotFound => new InvalidOperationException("Route introuvable (404)."),
            HttpStatusCode.UnsupportedMediaType => new InvalidOperationException("Format non supporte (415). Verifie que tu envoies bien du JSON."),
            HttpStatusCode.Conflict when IsStockInsufficient(body) => new InvalidOperationException(StockInsufficientMessage()),
            HttpStatusCode.Conflict => new InvalidOperationException(string.IsNullOrWhiteSpace(apiMessage) ? "Conflit (409)." : apiMessage),
            _ => new InvalidOperationException($"Erreur API ({(int)res.StatusCode}) : {apiMessage}")
        };
    }

    private static string ExtractApiMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("error", out var errorProp))
            {
                var value = errorProp.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch
        {
            // Keep original body when response is not JSON.
        }

        return body;
    }

    private static bool IsStockInsufficient(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (json.RootElement.TryGetProperty("code", out var codeProp) &&
                    string.Equals(codeProp.GetString(), "stock_insufficient", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (json.RootElement.TryGetProperty("error", out var errorProp) &&
                    errorProp.GetString()?.Contains("stock insuffisant", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Fallback below for non-JSON payloads.
        }

        return body.Contains("stock insuffisant", StringComparison.OrdinalIgnoreCase);
    }

    private static string StockInsufficientMessage()
    {
        var isEnglish = string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "en",
            StringComparison.OrdinalIgnoreCase);

        return isEnglish
            ? "Insufficient stock for at least one product. Please update your cart."
            : "Stock insuffisant pour au moins un produit. Mettez a jour le panier.";
    }
}
