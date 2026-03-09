using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public sealed class SearchAnalyticsCalculator
{
    private readonly DoorMarketDbContext _db;

    public SearchAnalyticsCalculator(DoorMarketDbContext db)
    {
        _db = db;
    }

    public async Task<SearchAnalyticsKpis> GetKpisAsync(
        DateTime? from,
        DateTime? to,
        string? source,
        CancellationToken ct)
    {
        var events = BuildFilteredEventsQuery(from, to, source);
        var queryEvents = events.Where(x => x.EventType == SearchAnalyticsEvents.Query);
        var clickEvents = events.Where(x => x.EventType == SearchAnalyticsEvents.Click);

        var searches = await queryEvents.CountAsync(ct);
        var clicks = await clickEvents.CountAsync(ct);
        var noResultSearches = await queryEvents
            .CountAsync(x => (x.ResultsCount ?? 0) <= 0, ct);
        var uniqueQueries = await queryEvents
            .Where(x => x.NormalizedQuery != null && x.NormalizedQuery != string.Empty)
            .Select(x => x.NormalizedQuery!)
            .Distinct()
            .CountAsync(ct);
        var uniqueUsers = await queryEvents
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(ct);
        var uniqueSessions = await queryEvents
            .Where(x => x.SessionId != null && x.SessionId != string.Empty)
            .Select(x => x.SessionId!)
            .Distinct()
            .CountAsync(ct);
        var avgResults = await queryEvents
            .Where(x => x.ResultsCount.HasValue)
            .Select(x => (double?)x.ResultsCount!.Value)
            .AverageAsync(ct);
        var avgLatencyMs = await queryEvents
            .Where(x => x.DurationMs.HasValue)
            .Select(x => (double?)x.DurationMs!.Value)
            .AverageAsync(ct);

        var ctr = searches <= 0
            ? 0m
            : decimal.Round((decimal)clicks * 100m / searches, 2, MidpointRounding.AwayFromZero);
        var noResultRate = searches <= 0
            ? 0m
            : decimal.Round((decimal)noResultSearches * 100m / searches, 2, MidpointRounding.AwayFromZero);

        return new SearchAnalyticsKpis(
            searches,
            clicks,
            ctr,
            noResultSearches,
            noResultRate,
            uniqueQueries,
            uniqueUsers,
            uniqueSessions,
            avgResults ?? 0d,
            avgLatencyMs ?? 0d);
    }

    public async Task<IReadOnlyList<TopSearchQueryRow>> GetTopQueriesAsync(
        DateTime? from,
        DateTime? to,
        string? source,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        var events = BuildFilteredEventsQuery(from, to, source);
        var queryEvents = events.Where(x => x.EventType == SearchAnalyticsEvents.Query);
        var clickEvents = events.Where(x => x.EventType == SearchAnalyticsEvents.Click);

        var topRows = await queryEvents
            .Where(x => x.NormalizedQuery != null && x.NormalizedQuery != string.Empty)
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                Query = g.Key,
                Searches = g.Count(),
                NoResultSearches = g.Sum(x => (x.ResultsCount ?? 0) <= 0 ? 1 : 0),
                AvgResults = g.Where(x => x.ResultsCount.HasValue)
                    .Select(x => (double?)x.ResultsCount!.Value)
                    .Average(),
                AvgLatencyMs = g.Where(x => x.DurationMs.HasValue)
                    .Select(x => (double?)x.DurationMs!.Value)
                    .Average(),
                LastSearchedAtUtc = g.Max(x => (DateTime?)x.OccurredAtUtc)
            })
            .OrderByDescending(x => x.Searches)
            .ThenBy(x => x.Query)
            .Take(take)
            .ToListAsync(ct);

        if (topRows.Count == 0)
        {
            return Array.Empty<TopSearchQueryRow>();
        }

        var keys = topRows.Select(x => x.Query).ToList();
        var clickCountByQuery = await clickEvents
            .Where(x => x.NormalizedQuery != null && keys.Contains(x.NormalizedQuery))
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new { Query = g.Key, Clicks = g.Count() })
            .ToDictionaryAsync(x => x.Query, x => x.Clicks, ct);

        return topRows
            .Select(x =>
            {
                clickCountByQuery.TryGetValue(x.Query, out var clicks);
                var ctr = x.Searches <= 0
                    ? 0m
                    : decimal.Round((decimal)clicks * 100m / x.Searches, 2, MidpointRounding.AwayFromZero);
                return new TopSearchQueryRow(
                    x.Query,
                    x.Searches,
                    clicks,
                    ctr,
                    x.NoResultSearches,
                    x.AvgResults ?? 0d,
                    x.AvgLatencyMs ?? 0d,
                    x.LastSearchedAtUtc);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<NoResultSearchQueryRow>> GetNoResultQueriesAsync(
        DateTime? from,
        DateTime? to,
        string? source,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        var events = BuildFilteredEventsQuery(from, to, source);
        var queryEvents = events.Where(x => x.EventType == SearchAnalyticsEvents.Query);

        var aggregates = await queryEvents
            .Where(x =>
                x.NormalizedQuery != null &&
                x.NormalizedQuery != string.Empty &&
                (x.ResultsCount ?? 0) <= 0)
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                Query = g.Key,
                Count = g.Count(),
                LastSearchedAtUtc = g.Max(x => (DateTime?)x.OccurredAtUtc)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Query)
            .Take(take)
            .ToListAsync(ct);

        return aggregates
            .Select(x => new NoResultSearchQueryRow(
                x.Query,
                x.Count,
                x.LastSearchedAtUtc))
            .ToList();
    }

    private IQueryable<Domain.Entities.SearchAnalyticsEvent> BuildFilteredEventsQuery(
        DateTime? from,
        DateTime? to,
        string? source)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var normalizedSource = NormalizeSource(source);

        var query = _db.SearchAnalyticsEvents.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc < toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSource))
        {
            query = query.Where(x => x.Source == normalizedSource);
        }

        return query;
    }

    private static (DateTime? fromUtc, DateTime? toUtc) NormalizeRange(DateTime? from, DateTime? to)
    {
        DateTime? fromUtc = from?.ToUniversalTime();
        DateTime? toUtc = to?.ToUniversalTime();

        if (toUtc.HasValue && toUtc.Value.TimeOfDay == TimeSpan.Zero)
        {
            toUtc = toUtc.Value.AddDays(1);
        }

        if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return (fromUtc, toUtc);
    }

    private static string? NormalizeSource(string? source)
    {
        var normalized = (source ?? string.Empty).Trim();
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

        return normalized.Length <= 30 ? normalized : normalized[..30];
    }

    public sealed record SearchAnalyticsKpis(
        int Searches,
        int Clicks,
        decimal ClickThroughRate,
        int NoResultSearches,
        decimal NoResultRate,
        int UniqueQueries,
        int UniqueUsers,
        int UniqueSessions,
        double AvgResultsCount,
        double AvgLatencyMs);

    public sealed record TopSearchQueryRow(
        string Query,
        int Searches,
        int Clicks,
        decimal ClickThroughRate,
        int NoResultSearches,
        double AvgResultsCount,
        double AvgLatencyMs,
        DateTime? LastSearchedAtUtc);

    public sealed record NoResultSearchQueryRow(
        string Query,
        int Count,
        DateTime? LastSearchedAtUtc);
}
