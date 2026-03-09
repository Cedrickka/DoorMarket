using System.Collections.Concurrent;

namespace DoorMarket.Api.Middlewares;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "__CorrelationId";

    private static readonly ConcurrentDictionary<char, byte> AllowedChars = BuildAllowedChars();

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    public static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var existing) &&
            existing is string existingId &&
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var incoming = context.Request.Headers[HeaderName].ToString();
        var normalizedIncoming = NormalizeIncoming(incoming);
        var correlationId = !string.IsNullOrWhiteSpace(normalizedIncoming)
            ? normalizedIncoming
            : context.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Items[ItemKey] = correlationId;
        return correlationId;
    }

    private static string? NormalizeIncoming(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Length > 96)
        {
            raw = raw[..96];
        }

        foreach (var c in raw)
        {
            if (!AllowedChars.ContainsKey(c))
            {
                return null;
            }
        }

        return raw;
    }

    private static ConcurrentDictionary<char, byte> BuildAllowedChars()
    {
        var map = new ConcurrentDictionary<char, byte>();
        for (var c = 'a'; c <= 'z'; c++)
        {
            map[c] = 1;
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            map[c] = 1;
        }

        for (var c = '0'; c <= '9'; c++)
        {
            map[c] = 1;
        }

        map['-'] = 1;
        map['_'] = 1;
        map['.'] = 1;
        return map;
    }
}
