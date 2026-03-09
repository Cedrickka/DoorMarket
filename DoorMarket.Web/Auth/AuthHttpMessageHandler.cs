using System.Net.Http.Headers;

namespace DoorMarket.Web.Auth;

public class AuthHttpMessageHandler : DelegatingHandler
{
    private readonly TokenStore _store;

    public AuthHttpMessageHandler(TokenStore store) => _store = store;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _store.Peek();

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, ct);
    }
}
