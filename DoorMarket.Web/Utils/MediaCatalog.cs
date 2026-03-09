namespace DoorMarket.Web.Utils;

public static class MediaCatalog
{
    private static string? _proxyHost;
    private static string? _apiBaseUrl;
    private static bool _proxyEnabled;

    public static void ConfigureProxy(string? apiBaseUrl, bool enableProxy = true)
    {
        if (!enableProxy || string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            _proxyEnabled = false;
            _proxyHost = null;
            _apiBaseUrl = null;
            return;
        }

        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
        {
            _proxyEnabled = false;
            _proxyHost = null;
            _apiBaseUrl = null;
            return;
        }

        _proxyEnabled = true;
        _proxyHost = apiUri.Host;
        _apiBaseUrl = apiUri.ToString();
    }

    private const string TomatoImage = "https://images.unsplash.com/photo-1546094096-0df4bcaaa337?auto=format&fit=crop&w=900&q=80";
    private const string BananaImage = "https://images.unsplash.com/photo-1574226516831-e1dff420e37f?auto=format&fit=crop&w=900&q=80";
    private const string AvocadoImage = "https://images.unsplash.com/photo-1523049673857-eb18f1d7b578?auto=format&fit=crop&w=900&q=80";
    private const string BreadImage = "https://images.unsplash.com/photo-1549931319-a545dcf3bc73?auto=format&fit=crop&w=900&q=80";
    private const string FishImage = "https://images.unsplash.com/photo-1615141982883-c7ad0e69fd62?auto=format&fit=crop&w=900&q=80";
    private const string MeatImage = "https://images.unsplash.com/photo-1607623814075-e51df1bdc82f?auto=format&fit=crop&w=900&q=80";
    private const string BeverageImage = "https://images.unsplash.com/photo-1543253687-c931c8e01820?auto=format&fit=crop&w=900&q=80";
    private const string SpicesImage = "https://images.unsplash.com/photo-1532336414038-cf19250c5757?auto=format&fit=crop&w=900&q=80";
    private const string GroceryShopImage = "https://images.unsplash.com/photo-1604719312566-8912e9227c6a?auto=format&fit=crop&w=1200&q=80";
    private const string FashionShopImage = "https://images.unsplash.com/photo-1512436991641-6745cdb1723f?auto=format&fit=crop&w=1200&q=80";
    private const string GenericShopImage = "https://images.unsplash.com/photo-1472851294608-062f824d29cc?auto=format&fit=crop&w=1200&q=80";

    public static string Product(string? mainImageUrl, string? name, string? categoryName, Guid productId)
    {
        if (!string.IsNullOrWhiteSpace(mainImageUrl))
        {
            if (Uri.TryCreate(mainImageUrl, UriKind.Absolute, out var absolute))
            {
                return ProxyIfNeeded(absolute.ToString());
            }

            var relative = mainImageUrl.StartsWith("/", StringComparison.Ordinal)
                ? mainImageUrl
                : "/" + mainImageUrl;

            return ProxyIfNeeded(relative);
        }

        var key = $"{categoryName} {name}".ToLowerInvariant();

        if (key.Contains("tomate") || key.Contains("tomato"))
        {
            return TomatoImage;
        }

        if (key.Contains("banane") || key.Contains("banana"))
        {
            return BananaImage;
        }

        if (key.Contains("avocat") || key.Contains("avocado"))
        {
            return AvocadoImage;
        }

        if (key.Contains("pain") || key.Contains("bread") || key.Contains("baguette"))
        {
            return BreadImage;
        }

        if (key.Contains("poisson") || key.Contains("fish"))
        {
            return FishImage;
        }

        if (key.Contains("viande") || key.Contains("beef") || key.Contains("meat"))
        {
            return MeatImage;
        }

        if (key.Contains("boisson") || key.Contains("drink") || key.Contains("juice"))
        {
            return BeverageImage;
        }

        if (key.Contains("epice") || key.Contains("spice"))
        {
            return SpicesImage;
        }

        return $"https://picsum.photos/seed/dm-product-{productId:N}/900/620";
    }

    public static string Shop(string? imageUrl, string? name, Guid shopId)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute))
            {
                return AppendCacheBuster(ProxyIfNeeded(absolute.ToString()));
            }

            var relative = imageUrl.StartsWith("/", StringComparison.Ordinal)
                ? imageUrl
                : "/" + imageUrl;

            return AppendCacheBuster(ProxyIfNeeded(relative));
        }

        var key = (name ?? string.Empty).ToLowerInvariant();

        if (key.Contains("market") || key.Contains("epicerie") || key.Contains("grocery"))
        {
            return GroceryShopImage;
        }

        if (key.Contains("mode") || key.Contains("fashion") || key.Contains("style"))
        {
            return FashionShopImage;
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            return $"https://picsum.photos/seed/dm-shop-{shopId:N}/1200/700";
        }

        return GenericShopImage;
    }

    public static string Shop(string? name, Guid shopId)
        => Shop(null, name, shopId);

    private static string AppendCacheBuster(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var hash = ComputeStableHash(url);
        var separator = url.Contains("?", StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}v={hash}";
    }

    private static string ComputeStableHash(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).Substring(0, 10).ToLowerInvariant();
    }

    private static string ProxyIfNeeded(string url)
    {
        if (!_proxyEnabled || string.IsNullOrWhiteSpace(_proxyHost))
        {
            return url;
        }

        Uri? absolute = null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
        {
            absolute = abs;
        }
        else if (!string.IsNullOrWhiteSpace(_apiBaseUrl) &&
                 Uri.TryCreate(_apiBaseUrl, UriKind.Absolute, out var baseUri))
        {
            var relative = url.StartsWith("/", StringComparison.Ordinal) ? url : "/" + url;
            absolute = new Uri(baseUri, relative);
        }

        if (absolute is null)
        {
            return url;
        }

        if (!string.Equals(absolute.Host, _proxyHost, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var builder = new UriBuilder(absolute);
        if (string.Equals(builder.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = Uri.UriSchemeHttp;
            if (builder.Port == 443)
            {
                builder.Port = -1;
            }
        }

        if (!string.Equals(builder.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var encoded = Uri.EscapeDataString(builder.Uri.ToString());
        return $"/media/proxy?url={encoded}";
    }
}
