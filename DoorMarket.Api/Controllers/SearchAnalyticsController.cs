using DoorMarket.Api.Services;
using DoorMarket.Api.Utils;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/search/analytics")]
[AllowAnonymous]
public class SearchAnalyticsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public SearchAnalyticsController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpPost("track")]
    public async Task<ActionResult<SearchAnalyticsTrackedDto>> Track(
        [FromBody] TrackSearchEventRequest request,
        CancellationToken ct = default)
    {
        var normalizedEvent = NormalizeEventName(request.EventName);
        var isClick = normalizedEvent is not null
            ? IsClickEvent(normalizedEvent)
            : (!string.IsNullOrWhiteSpace(request.TargetType) || request.TargetId.HasValue);

        if (isClick)
        {
            var clickResult = await PersistClickEventAsync(
                request.Query,
                request.TargetType,
                request.TargetId,
                request.Position,
                request.Page,
                request.Sort,
                request.FiltersHash,
                request.SessionId,
                request.Source,
                request.CountryTag,
                ct);

            if (clickResult is null)
            {
                return BadRequest("TargetType invalide ou TargetId manquant.");
            }

            return Ok(ToTrackedDto(clickResult));
        }

        var queryRow = await PersistQueryEventAsync(
            request.Query,
            request.ResultsCount,
            request.DurationMs,
            request.Page,
            request.Sort,
            request.FiltersHash,
            request.SessionId,
            request.Source,
            request.CountryTag,
            ct);

        return Ok(ToTrackedDto(queryRow));
    }

    [HttpPost("query")]
    public async Task<ActionResult<SearchAnalyticsTrackedDto>> TrackQuery(
        [FromBody] TrackSearchQueryRequest request,
        CancellationToken ct = default)
    {
        var eventRow = await PersistQueryEventAsync(
            request.Query,
            request.ResultsCount,
            request.DurationMs,
            request.Page,
            request.Sort,
            request.FiltersHash,
            request.SessionId,
            request.Source,
            request.CountryTag,
            ct);

        return Ok(ToTrackedDto(eventRow));
    }

    [HttpPost("click")]
    public async Task<ActionResult<SearchAnalyticsTrackedDto>> TrackClick(
        [FromBody] TrackSearchClickRequest request,
        CancellationToken ct = default)
    {
        var eventRow = await PersistClickEventAsync(
            request.Query,
            request.TargetType,
            request.TargetId,
            request.Position,
            request.Page,
            request.Sort,
            request.FiltersHash,
            request.SessionId,
            request.Source,
            request.CountryTag,
            ct);
        if (eventRow is null)
        {
            return BadRequest("TargetType invalide. Valeurs autorisees: Product, Shop, Category. TargetId requis.");
        }

        return Ok(ToTrackedDto(eventRow));
    }

    private async Task<Domain.Entities.SearchAnalyticsEvent> PersistQueryEventAsync(
        string? query,
        int? resultsCount,
        int? durationMs,
        int? page,
        string? sort,
        string? filtersHash,
        string? sessionId,
        string? source,
        string? countryTag,
        CancellationToken ct)
    {
        var eventRow = new Domain.Entities.SearchAnalyticsEvent
        {
            UserId = _current.UserId,
            SessionId = NormalizeText(sessionId, 80),
            EventType = SearchAnalyticsEvents.Query,
            Query = NormalizeText(query, 120),
            NormalizedQuery = NormalizeQuery(query),
            TargetType = null,
            TargetId = null,
            Position = null,
            ResultsCount = NormalizePositiveOrZero(resultsCount),
            DurationMs = NormalizePositiveOrZero(durationMs),
            Page = NormalizePage(page),
            Sort = NormalizeText(sort, 40),
            FiltersHash = NormalizeText(filtersHash, 80),
            Source = NormalizeSource(source),
            CountryTag = NormalizeCountryTag(countryTag),
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.SearchAnalyticsEvents.Add(eventRow);
        await _db.SaveChangesAsync(ct);
        return eventRow;
    }

    private async Task<Domain.Entities.SearchAnalyticsEvent?> PersistClickEventAsync(
        string? query,
        string? targetType,
        Guid? targetId,
        int? position,
        int? page,
        string? sort,
        string? filtersHash,
        string? sessionId,
        string? source,
        string? countryTag,
        CancellationToken ct)
    {
        var normalizedTargetType = NormalizeTargetType(targetType);
        if (normalizedTargetType is null || !targetId.HasValue || targetId.Value == Guid.Empty)
        {
            return null;
        }

        var eventRow = new Domain.Entities.SearchAnalyticsEvent
        {
            UserId = _current.UserId,
            SessionId = NormalizeText(sessionId, 80),
            EventType = SearchAnalyticsEvents.Click,
            Query = NormalizeText(query, 120),
            NormalizedQuery = NormalizeQuery(query),
            TargetType = normalizedTargetType,
            TargetId = targetId.Value,
            Position = NormalizePosition(position),
            ResultsCount = null,
            DurationMs = null,
            Page = NormalizePage(page),
            Sort = NormalizeText(sort, 40),
            FiltersHash = NormalizeText(filtersHash, 80),
            Source = NormalizeSource(source),
            CountryTag = NormalizeCountryTag(countryTag),
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.SearchAnalyticsEvents.Add(eventRow);
        await _db.SaveChangesAsync(ct);
        return eventRow;
    }

    private static SearchAnalyticsTrackedDto ToTrackedDto(Domain.Entities.SearchAnalyticsEvent row)
        => new(
            row.Id,
            row.EventType,
            row.OccurredAtUtc,
            row.NormalizedQuery);

    private static string? NormalizeEventName(string? value)
        => SearchTextNormalizer.NormalizeLookup(value, 80);

    private static bool IsClickEvent(string normalizedEvent)
        => normalizedEvent switch
        {
            "search_result_clicked" => true,
            "search_suggestion_clicked" => true,
            "search_shop_clicked" => true,
            "search_category_clicked" => true,
            _ => false
        };

    private static string? NormalizeQuery(string? value)
    {
        return SearchTextNormalizer.NormalizeLookup(value, 120);
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        return SearchTextNormalizer.NormalizeWords(value, maxLength);
    }

    private static int? NormalizePositiveOrZero(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 0, 120_000);
    }

    private static int? NormalizePage(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 1, 500);
    }

    private static int? NormalizePosition(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 1, 500);
    }

    private static string? NormalizeCountryTag(string? value)
        => NormalizeText(value, 8)?.ToUpperInvariant();

    private static string? NormalizeSource(string? value)
    {
        var normalized = NormalizeText(value, 30);
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

        if (string.Equals(normalized, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        return normalized;
    }

    private static string? NormalizeTargetType(string? value)
    {
        var normalized = NormalizeText(value, 20);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, SearchAnalyticsEvents.TargetProduct, StringComparison.OrdinalIgnoreCase))
        {
            return SearchAnalyticsEvents.TargetProduct;
        }

        if (string.Equals(normalized, SearchAnalyticsEvents.TargetShop, StringComparison.OrdinalIgnoreCase))
        {
            return SearchAnalyticsEvents.TargetShop;
        }

        if (string.Equals(normalized, SearchAnalyticsEvents.TargetCategory, StringComparison.OrdinalIgnoreCase))
        {
            return SearchAnalyticsEvents.TargetCategory;
        }

        return null;
    }

    public sealed record TrackSearchQueryRequest(
        string? Query,
        int? ResultsCount,
        int? DurationMs,
        int? Page,
        string? Sort,
        string? FiltersHash,
        string? SessionId,
        string? Source,
        string? CountryTag);

    public sealed record TrackSearchClickRequest(
        string? Query,
        string? TargetType,
        Guid? TargetId,
        int? Position,
        int? Page,
        string? Sort,
        string? FiltersHash,
        string? SessionId,
        string? Source,
        string? CountryTag);

    public sealed record TrackSearchEventRequest(
        string? EventName,
        string? Query,
        string? TargetType,
        Guid? TargetId,
        int? Position,
        int? ResultsCount,
        int? DurationMs,
        int? Page,
        string? Sort,
        string? FiltersHash,
        string? SessionId,
        string? Source,
        string? CountryTag);

    public sealed record SearchAnalyticsTrackedDto(
        Guid EventId,
        string EventType,
        DateTime OccurredAtUtc,
        string? NormalizedQuery);
}
