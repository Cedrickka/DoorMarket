namespace DoorMarket.Mobile.Utils;

public static class MediaCatalog
{
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
                return absolute.ToString();
            }

            return mainImageUrl.StartsWith("/", StringComparison.Ordinal)
                ? mainImageUrl
                : "/" + mainImageUrl;
        }

        var key = $"{categoryName} {name}".ToLowerInvariant();

        if (key.Contains("tomate") || key.Contains("tomato")) return TomatoImage;
        if (key.Contains("banane") || key.Contains("banana")) return BananaImage;
        if (key.Contains("avocat") || key.Contains("avocado")) return AvocadoImage;
        if (key.Contains("pain") || key.Contains("bread") || key.Contains("baguette")) return BreadImage;
        if (key.Contains("poisson") || key.Contains("fish")) return FishImage;
        if (key.Contains("viande") || key.Contains("beef") || key.Contains("meat")) return MeatImage;
        if (key.Contains("boisson") || key.Contains("drink") || key.Contains("juice")) return BeverageImage;
        if (key.Contains("epice") || key.Contains("spice")) return SpicesImage;

        return $"https://picsum.photos/seed/dm-product-{productId:N}/900/620";
    }

    public static string Shop(string? imageUrl, string? name, Guid shopId)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute))
            {
                return AppendCacheBuster(absolute.ToString());
            }

            var relative = imageUrl.StartsWith("/", StringComparison.Ordinal)
                ? imageUrl
                : "/" + imageUrl;

            return AppendCacheBuster(relative);
        }

        var key = (name ?? string.Empty).ToLowerInvariant();
        if (key.Contains("market") || key.Contains("epicerie") || key.Contains("grocery")) return GroceryShopImage;
        if (key.Contains("mode") || key.Contains("fashion") || key.Contains("style")) return FashionShopImage;
        if (!string.IsNullOrWhiteSpace(key)) return $"https://picsum.photos/seed/dm-shop-{shopId:N}/1200/700";
        return GenericShopImage;
    }

    public static string Shop(string? name, Guid shopId)
        => Shop(null, name, shopId);

    public static string Profile(string? email)
    {
        var seed = string.IsNullOrWhiteSpace(email) ? "client" : Uri.EscapeDataString(email.Trim().ToLowerInvariant());
        return $"https://i.pravatar.cc/200?u={seed}";
    }

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
}
