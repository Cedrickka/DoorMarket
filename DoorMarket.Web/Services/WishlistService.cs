using Blazored.LocalStorage;

namespace DoorMarket.Web.Services;

public sealed class WishlistService
{
    private const string StorageKey = "dm_wishlist_v1";
    private readonly ILocalStorageService _storage;

    public WishlistService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public event Action? OnChange;

    public async Task<IReadOnlyList<WishlistItem>> GetItemsAsync()
    {
        var items = await _storage.GetItemAsync<List<WishlistItem>>(StorageKey) ?? new List<WishlistItem>();
        return items
            .OrderByDescending(x => x.AddedAtUtc)
            .ToList();
    }

    public async Task<bool> ContainsAsync(Guid productId)
    {
        var items = await GetItemsAsync();
        return items.Any(x => x.ProductId == productId);
    }

    public async Task<bool> ToggleAsync(WishlistItemInput item)
    {
        var list = (await GetItemsAsync()).ToList();
        var existing = list.FirstOrDefault(x => x.ProductId == item.ProductId);
        if (existing is not null)
        {
            list.Remove(existing);
            await SaveAsync(list);
            return false;
        }

        list.Insert(0, new WishlistItem
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            ShopId = item.ShopId,
            ShopName = item.ShopName,
            Price = item.Price,
            EffectivePrice = item.EffectivePrice,
            HasActivePromotion = item.HasActivePromotion,
            Currency = item.Currency,
            MainImageUrl = item.MainImageUrl,
            AddedAtUtc = DateTime.UtcNow
        });
        await SaveAsync(list);
        return true;
    }

    public async Task RemoveAsync(Guid productId)
    {
        var list = (await GetItemsAsync())
            .Where(x => x.ProductId != productId)
            .ToList();
        await SaveAsync(list);
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(StorageKey);
        OnChange?.Invoke();
    }

    private async Task SaveAsync(List<WishlistItem> list)
    {
        await _storage.SetItemAsync(StorageKey, list);
        OnChange?.Invoke();
    }
}

public sealed class WishlistItem
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
    public DateTime AddedAtUtc { get; set; }
}

public sealed record WishlistItemInput(
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
