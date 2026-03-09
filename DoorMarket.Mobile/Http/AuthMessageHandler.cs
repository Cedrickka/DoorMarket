using System.Net;
using System.Net.Http.Headers;
using DoorMarket.Mobile.Auth;

namespace DoorMarket.Mobile.Http;

public sealed class AuthMessageHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<bool> RetryKey = new("dm-retried");
    public static readonly HttpRequestOptionsKey<bool> SkipRefreshKey = new("dm-no-refresh");

    private readonly TokenStore _tokens;
    private readonly AuthSession _session;

    public AuthMessageHandler(TokenStore tokens, AuthSession session)
    {
        _tokens = tokens;
        _session = session;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!IsAuthEndpoint(request))
        {
            var token = await _tokens.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (IsAuthEndpoint(request))
            return response;

        if (request.Options.TryGetValue(SkipRefreshKey, out var skipRefresh) && skipRefresh)
            return response;

        if (request.Options.TryGetValue(RetryKey, out var retried) && retried)
            return response;

        var refreshed = await _session.TryRefreshAsync(ct);
        if (!refreshed)
            return response;

        response.Dispose();

        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Options.Set(RetryKey, true);

        var newToken = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(newToken))
        {
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        }

        return await base.SendAsync(retryRequest, ct);
    }

    private static bool IsAuthEndpoint(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            var content = new ByteArrayContent(bytes);

            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}
