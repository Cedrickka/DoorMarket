using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace DoorMarket.Web.UI;

public static class WebErrorUx
{
    private static readonly Regex ApiErrorRegex = new(
        @"API\s+(?<status>\d{3})[^\:]*\:\s*(?<body>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StatusCodeRegex = new(
        @"status\s+code\s+(?<status>\d{3})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ForException(
        Exception ex,
        IStringLocalizer<SharedResource> sl,
        string? fallbackFr = null,
        string? fallbackEn = null)
    {
        if (ex is TaskCanceledException or OperationCanceledException)
        {
            return Localized(sl["ApiTimeout"], "La requete a expire. Verifiez la connexion.", "Request timed out. Check your connection.");
        }

        if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
        {
            return FromApiBody(httpEx.StatusCode.Value, null, sl, fallbackFr, fallbackEn);
        }

        var message = ex.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return Fallback(sl, fallbackFr, fallbackEn);
        }

        var apiMatch = ApiErrorRegex.Match(message);
        if (apiMatch.Success &&
            int.TryParse(apiMatch.Groups["status"].Value, out var apiStatus))
        {
            var body = apiMatch.Groups["body"].Value;
            return FromApiBody((HttpStatusCode)apiStatus, body, sl, fallbackFr, fallbackEn);
        }

        var statusMatch = StatusCodeRegex.Match(message);
        if (statusMatch.Success &&
            int.TryParse(statusMatch.Groups["status"].Value, out var statusCode))
        {
            return FromApiBody((HttpStatusCode)statusCode, null, sl, fallbackFr, fallbackEn);
        }

        var normalized = NormalizeRawMessage(message);
        return string.IsNullOrWhiteSpace(normalized) ? Fallback(sl, fallbackFr, fallbackEn) : normalized;
    }

    public static async Task<string> ForHttpFailureAsync(
        HttpResponseMessage response,
        IStringLocalizer<SharedResource> sl,
        string? fallbackFr = null,
        string? fallbackEn = null)
    {
        var body = await response.Content.ReadAsStringAsync();
        return FromApiBody(response.StatusCode, body, sl, fallbackFr, fallbackEn);
    }

    public static string FromApiBody(
        HttpStatusCode statusCode,
        string? body,
        IStringLocalizer<SharedResource> sl,
        string? fallbackFr = null,
        string? fallbackEn = null)
    {
        var payload = ParsePayload(body);

        if (IsStockInsufficient(payload))
        {
            return Localized(
                sl["StockInsufficientFriendly"],
                "Stock insuffisant pour au moins un produit. Mettez a jour le panier.",
                "Insufficient stock for at least one product. Please update your cart.");
        }

        var fallback = Fallback(sl, fallbackFr, fallbackEn);
        var apiLabel = Localized(sl["ApiError"], "Erreur API", "API error");

        string message = statusCode switch
        {
            HttpStatusCode.Unauthorized => Tr(
                "Session expiree ou non autorisee. Reconnectez-vous.",
                "Session expired or unauthorized. Please sign in again."),
            HttpStatusCode.Forbidden => Tr(
                "Acces refuse pour cette action.",
                "Access denied for this action."),
            HttpStatusCode.NotFound => Tr(
                "Ressource introuvable.",
                "Resource not found."),
            HttpStatusCode.Conflict => !string.IsNullOrWhiteSpace(payload.Message)
                ? payload.Message!
                : Tr("Conflit de donnees. Rechargez puis reessayez.", "Data conflict. Refresh and retry."),
            HttpStatusCode.TooManyRequests => Tr(
                "Trop de requetes. Reessayez dans quelques instants.",
                "Too many requests. Please retry in a moment."),
            HttpStatusCode.BadRequest => !string.IsNullOrWhiteSpace(payload.Message)
                ? payload.Message!
                : fallback,
            HttpStatusCode.UnprocessableEntity => !string.IsNullOrWhiteSpace(payload.Message)
                ? payload.Message!
                : fallback,
            _ when (int)statusCode >= 500 => Tr(
                "Incident serveur temporaire. Reessayez plus tard.",
                "Temporary server issue. Please retry later."),
            _ => !string.IsNullOrWhiteSpace(payload.Message)
                ? payload.Message!
                : $"{apiLabel} ({(int)statusCode})."
        };

        if (!string.IsNullOrWhiteSpace(payload.CorrelationId))
        {
            message = $"{message} ({Tr("Ref", "Ref")}: {payload.CorrelationId})";
        }

        return NormalizeRawMessage(message);
    }

    private static string NormalizeRawMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length <= 700)
        {
            return trimmed;
        }

        return trimmed[..700] + "...";
    }

    private static bool IsStockInsufficient(ApiPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Code) &&
            string.Equals(payload.Code, "stock_insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(payload.Message) &&
            payload.Message.Contains("stock insuffisant", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static ApiPayload ParsePayload(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ApiPayload(null, null, null);
        }

        var trimmed = body.Trim();
        var jsonStart = trimmed.IndexOf('{');
        if (jsonStart > 0 && trimmed.StartsWith("API", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[jsonStart..];
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ApiPayload(trimmed, null, null);
            }

            var root = doc.RootElement;
            var message = FirstNonEmpty(
                GetString(root, "error"),
                GetString(root, "message"),
                GetString(root, "detail"),
                GetString(root, "title"),
                ReadValidationErrors(root));

            var code = FirstNonEmpty(GetString(root, "code"));
            var correlationId = FirstNonEmpty(
                GetString(root, "correlationId"),
                GetString(root, "traceId"),
                GetString(root, "requestId"));

            return new ApiPayload(message, code, correlationId);
        }
        catch
        {
            return new ApiPayload(trimmed, null, null);
        }
    }

    private static string? ReadValidationErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors))
        {
            return null;
        }

        if (errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in errors.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        if (errors.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in errors.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var text = item.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string Fallback(IStringLocalizer<SharedResource> sl, string? fr, string? en)
    {
        if (!string.IsNullOrWhiteSpace(fr) || !string.IsNullOrWhiteSpace(en))
        {
            return Tr(fr ?? "Operation impossible.", en ?? "Operation failed.");
        }

        return Tr("Operation impossible. Reessayez.", "Operation failed. Please retry.");
    }

    private static string Localized(LocalizedString value, string frFallback, string enFallback)
    {
        if (!value.ResourceNotFound && !string.IsNullOrWhiteSpace(value.Value))
        {
            return value.Value;
        }

        return Tr(frFallback, enFallback);
    }

    private static string Tr(string fr, string en)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }

    private sealed record ApiPayload(string? Message, string? Code, string? CorrelationId);
}
