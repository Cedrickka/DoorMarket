using System.Globalization;
using System.Text;

namespace DoorMarket.Api.Utils;

public static class SearchTextNormalizer
{
    public static string? NormalizeWords(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = string.Join(' ', normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    public static string? NormalizeLookup(string? value, int maxLength)
    {
        var normalized = NormalizeWords(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return FoldDiacritics(normalized).ToLowerInvariant();
    }

    public static IReadOnlyList<string> Tokenize(string? value, int maxTokens = 6, int minTokenLength = 2)
    {
        var normalized = NormalizeLookup(value, 120);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = token.Trim();
            if (candidate.Length < minTokenLength)
            {
                continue;
            }

            if (candidate.Length > 40)
            {
                candidate = candidate[..40];
            }

            set.Add(candidate);
            if (set.Count >= maxTokens)
            {
                break;
            }
        }

        return set.ToList();
    }

    public static bool ContainsAllTokens(string? value, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return true;
        }

        var normalized = NormalizeLookup(value, 240);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!normalized.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string FoldDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
