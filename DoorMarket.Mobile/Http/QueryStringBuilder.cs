using System.Text;

namespace DoorMarket.Mobile.Http;

public static class QueryStringBuilder
{
    public static string AddQueryString(string path, IReadOnlyDictionary<string, string?> parameters)
    {
        if (parameters.Count == 0)
        {
            return path;
        }

        var builder = new StringBuilder(path);
        var hasQuery = path.Contains('?', StringComparison.Ordinal);

        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append(hasQuery ? '&' : '?');
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
            hasQuery = true;
        }

        return builder.ToString();
    }
}
