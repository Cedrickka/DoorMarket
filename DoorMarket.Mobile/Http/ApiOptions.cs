namespace DoorMarket.Mobile.Http;

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public Uri GetBaseUri()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("Api:BaseUrl manquant (appsettings.json).");

        var url = BaseUrl.Trim();
        if (!url.EndsWith("/")) url += "/";
        return new Uri(url);
    }
}
