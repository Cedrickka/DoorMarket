using System.Globalization;
using System.Net.Http.Headers;

namespace DoorMarket.Mobile.Http;

public sealed class CultureMessageHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var culture = CultureInfo.CurrentUICulture;
        var language = culture.TwoLetterISOLanguageName;

        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture.Name));

        if (request.Headers.Contains("X-App-Lang"))
        {
            request.Headers.Remove("X-App-Lang");
        }

        request.Headers.Add("X-App-Lang", language);
        return base.SendAsync(request, ct);
    }
}
