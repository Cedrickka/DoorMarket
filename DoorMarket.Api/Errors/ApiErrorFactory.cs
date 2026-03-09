using DoorMarket.Api.Middlewares;
using DoorMarket.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DoorMarket.Api.Errors;

public static class ApiErrorFactory
{
    public static ApiErrorResponse FromException(Exception exception, HttpContext context)
    {
        var (status, code, message) = MapException(exception);
        return Build(context, status, code, message, null);
    }

    public static ApiErrorResponse FromStatusCode(
        int statusCode,
        HttpContext context,
        object? value = null,
        string? overrideCode = null,
        object? details = null)
    {
        if (value is ApiErrorResponse alreadyNormalized)
        {
            return EnsureCorrelation(alreadyNormalized, context, statusCode);
        }

        var message = ExtractMessage(value);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = DefaultMessage(statusCode);
        }

        var code = string.IsNullOrWhiteSpace(overrideCode)
            ? MapCode(statusCode, message)
            : overrideCode;
        var resolvedDetails = details ?? ExtractDetails(value);
        return Build(context, statusCode, code, message, resolvedDetails);
    }

    private static ApiErrorResponse Build(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        object? details)
    {
        return new ApiErrorResponse
        {
            code = code,
            message = message,
            status = statusCode,
            correlationId = CorrelationIdMiddleware.GetOrCreateCorrelationId(context),
            path = context.Request.Path.Value,
            details = details
        };
    }

    private static ApiErrorResponse EnsureCorrelation(ApiErrorResponse value, HttpContext context, int fallbackStatus)
    {
        var status = value.status > 0 ? value.status : fallbackStatus;
        var message = string.IsNullOrWhiteSpace(value.message) ? DefaultMessage(status) : value.message;
        var code = string.IsNullOrWhiteSpace(value.code) ? MapCode(status, message) : value.code;
        var correlationId = string.IsNullOrWhiteSpace(value.correlationId)
            ? CorrelationIdMiddleware.GetOrCreateCorrelationId(context)
            : value.correlationId;

        return new ApiErrorResponse
        {
            code = code,
            message = message,
            status = status,
            correlationId = correlationId,
            path = value.path ?? context.Request.Path.Value,
            details = value.details
        };
    }

    private static (int Status, string Code, string Message) MapException(Exception exception)
    {
        var message = exception.Message;

        if (exception is UnauthorizedAccessException)
        {
            return (StatusCodes.Status401Unauthorized, "unauthorized", string.IsNullOrWhiteSpace(message) ? "Acces non autorise." : message);
        }

        if (exception is KeyNotFoundException)
        {
            return (StatusCodes.Status404NotFound, "not_found", string.IsNullOrWhiteSpace(message) ? "Ressource introuvable." : message);
        }

        if (exception is InvalidOperationException)
        {
            var code = MapCode(StatusCodes.Status400BadRequest, message);
            var status = code == "stock_insufficient" ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            return (status, code, string.IsNullOrWhiteSpace(message) ? DefaultMessage(status) : message);
        }

        if (exception is ArgumentException or FormatException)
        {
            return (
                StatusCodes.Status400BadRequest,
                MapCode(StatusCodes.Status400BadRequest, message),
                string.IsNullOrWhiteSpace(message) ? "Requete invalide." : message);
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return (StatusCodes.Status409Conflict, "concurrency_conflict", "Conflit de mise a jour. Reessayez.");
        }

        if (exception is TimeoutException)
        {
            return (StatusCodes.Status503ServiceUnavailable, "upstream_timeout", "Service temporairement indisponible.");
        }

        return (StatusCodes.Status500InternalServerError, "internal_error", "Une erreur interne est survenue.");
    }

    private static string MapCode(int statusCode, string? message)
    {
        var normalized = (message ?? string.Empty).ToLowerInvariant();
        if (statusCode == StatusCodes.Status400BadRequest)
        {
            if (normalized.Contains("minprice", StringComparison.Ordinal) ||
                normalized.Contains("maxprice", StringComparison.Ordinal) ||
                normalized.Contains("ratingmin", StringComparison.Ordinal))
            {
                return "search_filter_invalid";
            }

            if (normalized.Contains("coupon", StringComparison.Ordinal))
            {
                return "coupon_invalid";
            }

            if (normalized.Contains("zone de livraison", StringComparison.Ordinal) ||
                normalized.Contains("delivery zone", StringComparison.Ordinal))
            {
                return "delivery_zone_invalid";
            }

            if (normalized.Contains("panier vide", StringComparison.Ordinal))
            {
                return "cart_empty";
            }

            if (normalized.Contains("prepay", StringComparison.Ordinal) ||
                normalized.Contains("voucher", StringComparison.Ordinal))
            {
                return "prepaid_code_invalid";
            }

            if (normalized.Contains("stock insuffisant", StringComparison.Ordinal))
            {
                return "stock_insufficient";
            }

            return "bad_request";
        }

        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            return "unauthorized";
        }

        if (statusCode == StatusCodes.Status403Forbidden)
        {
            return "forbidden";
        }

        if (statusCode == StatusCodes.Status404NotFound)
        {
            return "not_found";
        }

        if (statusCode == StatusCodes.Status409Conflict)
        {
            return normalized.Contains("stock insuffisant", StringComparison.Ordinal)
                ? "stock_insufficient"
                : "conflict";
        }

        if (statusCode == StatusCodes.Status422UnprocessableEntity)
        {
            return "validation_failed";
        }

        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            return "rate_limited";
        }

        if (statusCode >= 500)
        {
            return "internal_error";
        }

        return "error";
    }

    private static string DefaultMessage(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Requete invalide.",
            StatusCodes.Status401Unauthorized => "Authentification requise.",
            StatusCodes.Status403Forbidden => "Acces refuse.",
            StatusCodes.Status404NotFound => "Ressource introuvable.",
            StatusCodes.Status409Conflict => "Conflit detecte.",
            StatusCodes.Status422UnprocessableEntity => "Validation echouee.",
            StatusCodes.Status429TooManyRequests => "Trop de requetes. Reessayez dans quelques instants.",
            StatusCodes.Status503ServiceUnavailable => "Service temporairement indisponible.",
            _ when statusCode >= 500 => "Une erreur interne est survenue.",
            _ => "Erreur de requete."
        };
    }

    private static string? ExtractMessage(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            ValidationProblemDetails validationProblemDetails => validationProblemDetails.Title ?? validationProblemDetails.Detail,
            ProblemDetails problemDetails => problemDetails.Detail ?? problemDetails.Title,
            _ => TryGetStringProperty(value, "message")
                 ?? TryGetStringProperty(value, "error")
                 ?? TryGetStringProperty(value, "title")
        };
    }

    private static object? ExtractDetails(object? value)
    {
        return value switch
        {
            ValidationProblemDetails validationProblemDetails => validationProblemDetails.Errors,
            ProblemDetails problemDetails => problemDetails.Extensions.Count == 0 ? null : problemDetails.Extensions,
            _ => TryGetObjectProperty(value, "details")
                 ?? TryGetObjectProperty(value, "errors")
        };
    }

    private static string? TryGetStringProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        var propertyValue = property?.GetValue(value);
        return propertyValue?.ToString();
    }

    private static object? TryGetObjectProperty(object? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        var property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(value);
    }
}
