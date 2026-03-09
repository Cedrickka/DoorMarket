using Blazored.LocalStorage;

namespace DoorMarket.Web.Services;

public sealed class GuestCartService
{
    private const string StorageKey = "dm_guest_cart_v1";
    private readonly ILocalStorageService _storage;

    public GuestCartService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public async Task<GuestCart> GetAsync()
    {
        var cart = await _storage.GetItemAsync<GuestCart>(StorageKey);
        if (cart is null)
        {
            return new GuestCart
            {
                Items = new List<GuestCartItem>(),
                Currency = "USD",
                Subtotal = 0m
            };
        }

        return Recalculate(cart);
    }

    public async Task<bool> HasItemsAsync()
    {
        var cart = await GetAsync();
        return cart.Items.Count > 0;
    }

    public async Task<GuestCart> AddItemAsync(GuestCartProduct product, int qty)
    {
        if (qty < 1) qty = 1;

        var cart = await GetAsync();
        var existing = cart.Items.FirstOrDefault(x => x.ProductId == product.ProductId);
        if (existing is not null)
        {
            existing.Qty += qty;
        }
        else
        {
            cart.Items.Add(new GuestCartItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                UnitPrice = product.UnitPrice,
                Qty = qty,
                Currency = product.Currency,
                MainImageUrl = product.MainImageUrl
            });
        }

        cart.Currency = string.IsNullOrWhiteSpace(cart.Currency) ? product.Currency : cart.Currency;
        return await SaveAsync(cart);
    }

    public async Task<GuestCart> UpdateQtyAsync(Guid itemId, int qty)
    {
        var cart = await GetAsync();
        var item = cart.Items.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
        {
            return cart;
        }

        item.Qty = Math.Max(1, qty);
        return await SaveAsync(cart);
    }

    public async Task<GuestCart> RemoveItemAsync(Guid itemId)
    {
        var cart = await GetAsync();
        cart.Items.RemoveAll(x => x.Id == itemId);
        return await SaveAsync(cart);
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(StorageKey);
    }

    public async Task MergeIntoUserCartAsync(CartApiClient cartApi, CancellationToken ct = default)
    {
        var cart = await GetAsync();
        if (cart.Items.Count == 0)
        {
            return;
        }

        foreach (var item in cart.Items)
        {
            await cartApi.AddItemAsync(item.ProductId, item.Qty, ct);
        }

        await ClearAsync();
    }

    private async Task<GuestCart> SaveAsync(GuestCart cart)
    {
        var updated = Recalculate(cart);
        await _storage.SetItemAsync(StorageKey, updated);
        return updated;
    }

    private static GuestCart Recalculate(GuestCart cart)
    {
        cart.Subtotal = cart.Items.Sum(x => x.UnitPrice * x.Qty);
        return cart;
    }
}

public sealed class GuestCart
{
    public List<GuestCartItem> Items { get; init; } = new();
    public string Currency { get; set; } = "USD";
    public decimal Subtotal { get; set; }
}

public sealed class GuestCartItem
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Qty { get; set; }
    public string Currency { get; init; } = "USD";
    public string? MainImageUrl { get; init; }
}

public record GuestCartProduct(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    string? MainImageUrl
);
