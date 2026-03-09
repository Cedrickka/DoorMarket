using Blazored.LocalStorage;

namespace DoorMarket.Web.Services;

public sealed class RecentlyViewedService
{
    private const string StorageKey = "dm_recently_viewed_products_v1";
    private const int MaxItems = 30;
    private readonly ILocalStorageService _storage;

    public RecentlyViewedService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public event Action? OnChange;

    public async Task<IReadOnlyList<RecentlyViewedProduct>> GetProductsAsync(int take = 12)
    {
        var list = await _storage.GetItemAsync<List<RecentlyViewedProduct>>(StorageKey) ?? new List<RecentlyViewedProduct>();
        var normalizedTake = take <= 0 ? 12 : take;
        return list
            .OrderByDescending(x => x.ViewedAtUtc)
            .Take(normalizedTake)
            .ToList();
    }

    public async Task AddProductAsync(RecentlyViewedProductInput input)
    {
        var list = await _storage.GetItemAsync<List<RecentlyViewedProduct>>(StorageKey) ?? new List<RecentlyViewedProduct>();

        list.RemoveAll(x => x.ProductId == input.ProductId);
        list.Insert(0, new RecentlyViewedProduct
        {
            ProductId = input.ProductId,
            ProductName = input.ProductName,
            ShopId = input.ShopId,
            ShopName = input.ShopName,
            Price = input.Price,
            EffectivePrice = input.EffectivePrice,
            HasActivePromotion = input.HasActivePromotion,
            Currency = input.Currency,
            MainImageUrl = input.MainImageUrl,
            ViewedAtUtc = DateTime.UtcNow
        });

        if (list.Count > MaxItems)
        {
            list = list.Take(MaxItems).ToList();
        }

        await _storage.SetItemAsync(StorageKey, list);
        OnChange?.Invoke();
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(StorageKey);
        OnChange?.Invoke();
    }
}

public sealed class RecentlyViewedProduct
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool HasActivePromotion { get; set; }
    public string Currency { get; set; } = "USD";
    public string? MainImageUrl { get; set; }
    public DateTime ViewedAtUtc { get; set; }
}

public sealed record RecentlyViewedProductInput(
    Guid ProductId,
    string ProductName,
    Guid ShopId,
    string ShopName,
    decimal Price,
    decimal EffectivePrice,
    bool HasActivePromotion,
    string Currency,
    string? MainImageUrl
);
