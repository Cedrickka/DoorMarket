using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using System.Text.Json;

namespace DoorMarket.Api.Services;

public interface ICheckoutObservabilityService
{
    Task<CheckoutAnalyticsEvent> TrackAsync(
        string eventType,
        Guid? orderId = null,
        string? sessionId = null,
        string? paymentProvider = null,
        string? paymentChannel = null,
        string? experimentName = null,
        string? experimentGroup = null,
        bool? success = null,
        int? durationMs = null,
        string? errorCode = null,
        string? errorMessage = null,
        string? source = null,
        string? countryTag = null,
        object? metadata = null,
        CancellationToken ct = default);
}

public sealed class CheckoutObservabilityService : ICheckoutObservabilityService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public CheckoutObservabilityService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CheckoutAnalyticsEvent> TrackAsync(
        string eventType,
        Guid? orderId = null,
        string? sessionId = null,
        string? paymentProvider = null,
        string? paymentChannel = null,
        string? experimentName = null,
        string? experimentGroup = null,
        bool? success = null,
        int? durationMs = null,
        string? errorCode = null,
        string? errorMessage = null,
        string? source = null,
        string? countryTag = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        var row = new CheckoutAnalyticsEvent
        {
            UserId = _current.UserId,
            OrderId = orderId,
            SessionId = Normalize(sessionId, 80),
            EventType = NormalizeEventType(eventType),
            PaymentProvider = Normalize(paymentProvider, 40),
            PaymentChannel = Normalize(paymentChannel, 40),
            ExperimentName = Normalize(experimentName, 50),
            ExperimentGroup = Normalize(experimentGroup, 16),
            Success = success,
            DurationMs = NormalizeDuration(durationMs),
            ErrorCode = Normalize(errorCode, 80),
            ErrorMessage = Normalize(errorMessage, 300),
            Source = NormalizeSource(source),
            CountryTag = NormalizeCountryTag(countryTag),
            MetadataJson = SerializeMetadata(metadata),
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.CheckoutAnalyticsEvents.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private static string NormalizeEventType(string? value)
    {
        var normalized = Normalize(value, 40);
        return string.IsNullOrWhiteSpace(normalized)
            ? CheckoutObservabilityEvents.CheckoutViewed
            : normalized;
    }

    private static int? NormalizeDuration(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 0, 300_000);
    }

    private static string? NormalizeSource(string? value)
    {
        var normalized = Normalize(value, 20);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, "web", StringComparison.OrdinalIgnoreCase))
        {
            return "Web";
        }

        if (string.Equals(normalized, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            return "Mobile";
        }

        if (string.Equals(normalized, "api", StringComparison.OrdinalIgnoreCase))
        {
            return "Api";
        }

        return normalized;
    }

    private static string? NormalizeCountryTag(string? value)
        => Normalize(value, 8)?.ToUpperInvariant();

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? SerializeMetadata(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(metadata);
            return json.Length <= 2000 ? json : json[..2000];
        }
        catch
        {
            return null;
        }
    }
}
