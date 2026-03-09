using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Services;

public sealed class SearchRuleSuggestionService
{
    private const string QueryEventType = "Query";
    private const string TargetTypeProduct = "Product";
    private const string TargetTypeShop = "Shop";
    private const string TargetTypeCategory = "Category";

    private readonly DoorMarketDbContext _db;

    public SearchRuleSuggestionService(DoorMarketDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SearchRuleSuggestion>> GetSuggestionsAsync(
        DateTime? from,
        DateTime? to,
        string? source,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        var (fromUtc, toUtc) = NormalizeRange(from, to);
        var normalizedSource = NormalizeSource(source);

        var noResultQuery = _db.SearchAnalyticsEvents.AsNoTracking()
            .Where(x => x.EventType == QueryEventType)
            .Where(x => x.NormalizedQuery != null && x.NormalizedQuery != string.Empty)
            .Where(x => (x.ResultsCount ?? 0) <= 0);

        if (fromUtc.HasValue)
        {
            noResultQuery = noResultQuery.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            noResultQuery = noResultQuery.Where(x => x.OccurredAtUtc < toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSource))
        {
            noResultQuery = noResultQuery.Where(x => x.Source == normalizedSource);
        }

        var groupedNoResultRows = await noResultQuery
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                Query = g.Key,
                Count = g.Count(),
                LastSeenAtUtc = g.Max(x => (DateTime?)x.OccurredAtUtc)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Query)
            .Take(take * 5)
            .ToListAsync(ct);

        var noResultRows = groupedNoResultRows
            .Select(x => new NoResultQueryStat(x.Query, x.Count, x.LastSeenAtUtc))
            .ToList();

        if (noResultRows.Count == 0)
        {
            return Array.Empty<SearchRuleSuggestion>();
        }

        var existingTriggers = await _db.SearchQueryRules.AsNoTracking()
            .Select(x => x.TriggerQuery)
            .ToListAsync(ct);
        var existingSet = existingTriggers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(1500)
            .Select(x => new CandidateItem(
                TargetTypeProduct,
                x.Id,
                x.Name,
                NormalizeName(x.Name)))
            .ToListAsync(ct);

        var shops = await _db.Shops.AsNoTracking()
            .OrderByDescending(x => x.IsVerified)
            .ThenBy(x => x.Name)
            .Take(800)
            .Select(x => new CandidateItem(
                TargetTypeShop,
                x.Id,
                x.Name,
                NormalizeName(x.Name)))
            .ToListAsync(ct);

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new CategoryCandidateRow(
                x.Id,
                x.Name,
                x.NameEn))
            .ToListAsync(ct);

        var candidateItems = new List<CandidateItem>(products.Count + shops.Count + (categories.Count * 2));
        candidateItems.AddRange(products);
        candidateItems.AddRange(shops);

        foreach (var category in categories)
        {
            candidateItems.Add(new CandidateItem(
                TargetTypeCategory,
                category.Id,
                category.Name,
                NormalizeName(category.Name)));

            if (!string.IsNullOrWhiteSpace(category.NameEn))
            {
                candidateItems.Add(new CandidateItem(
                    TargetTypeCategory,
                    category.Id,
                    category.Name,
                    NormalizeName(category.NameEn)));
            }
        }

        var suggestions = new List<SearchRuleSuggestion>();
        foreach (var row in noResultRows)
        {
            if (existingSet.Contains(row.Query))
            {
                continue;
            }

            var normalizedQuery = NormalizeName(row.Query);
            if (string.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery.Length < 2)
            {
                continue;
            }

            var best = FindBestCandidate(normalizedQuery, candidateItems);
            if (best is null || best.Score < 72)
            {
                continue;
            }

            var confidence = best.Score >= 92
                ? "High"
                : best.Score >= 82
                    ? "Medium"
                    : "Low";

            suggestions.Add(new SearchRuleSuggestion(
                TriggerQuery: row.Query,
                SuggestedCanonicalQuery: best.CanonicalQuery,
                TargetType: best.TargetType,
                TargetId: best.TargetId,
                TargetLabel: best.TargetLabel,
                Score: best.Score,
                Confidence: confidence,
                Reason: best.Reason,
                NoResultCount: row.Count,
                LastSeenAtUtc: row.LastSeenAtUtc));
        }

        return suggestions
            .OrderByDescending(x => x.NoResultCount)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.TriggerQuery)
            .Take(take)
            .ToList();
    }

    private static BestCandidate? FindBestCandidate(string query, IReadOnlyList<CandidateItem> candidates)
    {
        BestCandidate? best = null;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.NormalizedLabel))
            {
                continue;
            }

            var evaluation = EvaluateSimilarity(query, candidate.NormalizedLabel);
            if (evaluation.Score <= 0)
            {
                continue;
            }

            var current = new BestCandidate(
                candidate.Type,
                candidate.Id,
                candidate.Label,
                candidate.NormalizedLabel,
                evaluation.Score,
                evaluation.Reason);

            if (best is null || current.Score > best.Score)
            {
                best = current;
            }
        }

        return best;
    }

    private static SimilarityEvaluation EvaluateSimilarity(string query, string candidate)
    {
        if (query == candidate)
        {
            return new SimilarityEvaluation(100, "exact match");
        }

        var score = 0;
        var reason = "token overlap";

        if (candidate.StartsWith(query, StringComparison.Ordinal))
        {
            score = Math.Max(score, 90);
            reason = "candidate starts with query";
        }
        else if (query.StartsWith(candidate, StringComparison.Ordinal))
        {
            score = Math.Max(score, 86);
            reason = "query starts with candidate";
        }

        if (candidate.Contains(query, StringComparison.Ordinal) || query.Contains(candidate, StringComparison.Ordinal))
        {
            score = Math.Max(score, 80);
            reason = "contains overlap";
        }

        var queryTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (queryTokens.Length > 0 && candidateTokens.Length > 0)
        {
            var queryTokenSet = queryTokens.ToHashSet(StringComparer.Ordinal);
            var candidateTokenSet = candidateTokens.ToHashSet(StringComparer.Ordinal);
            var intersect = queryTokenSet.Intersect(candidateTokenSet, StringComparer.Ordinal).Count();
            if (intersect > 0)
            {
                var union = queryTokenSet.Union(candidateTokenSet, StringComparer.Ordinal).Count();
                var jaccard = union <= 0 ? 0d : (double)intersect / union;
                var tokenScore = (int)Math.Round(jaccard * 60d, MidpointRounding.AwayFromZero);
                if (tokenScore > score)
                {
                    score = tokenScore;
                    reason = "token overlap";
                }
            }
        }

        if (Math.Abs(query.Length - candidate.Length) <= 3)
        {
            var distance = LevenshteinDistance(query, candidate);
            if (distance <= 2)
            {
                var typoScore = 82 - (distance * 8);
                if (typoScore > score)
                {
                    score = typoScore;
                    reason = "possible typo";
                }
            }
        }

        return new SimilarityEvaluation(score, reason);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var dp = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) dp[i, 0] = i;
        for (var j = 0; j <= m; j++) dp[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(
                        dp[i - 1, j] + 1,
                        dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[n, m];
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        var compact = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= 120 ? compact : compact[..120];
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

    public sealed record SearchRuleSuggestion(
        string TriggerQuery,
        string? SuggestedCanonicalQuery,
        string TargetType,
        Guid TargetId,
        string TargetLabel,
        int Score,
        string Confidence,
        string Reason,
        int NoResultCount,
        DateTime? LastSeenAtUtc);

    private sealed record NoResultQueryStat(
        string Query,
        int Count,
        DateTime? LastSeenAtUtc);

    private sealed record CategoryCandidateRow(
        Guid Id,
        string Name,
        string? NameEn);

    private sealed record CandidateItem(
        string Type,
        Guid Id,
        string Label,
        string NormalizedLabel);

    private sealed record BestCandidate(
        string TargetType,
        Guid TargetId,
        string TargetLabel,
        string CanonicalQuery,
        int Score,
        string Reason);

    private sealed record SimilarityEvaluation(
        int Score,
        string Reason);
}
