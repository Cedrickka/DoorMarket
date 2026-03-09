using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.Carts;

public class CartService : ICartService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public CartService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CartDto> GetMyCartAsync(CancellationToken ct)
    {
        var cart = await GetOrCreateCartAsync(ct);
        return await MapCartAsync(cart.Id, ct);
    }

    public async Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct)
    {
        if (qty <= 0) throw new InvalidOperationException("Quantité invalide.");

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var cart = await GetOrCreateCartAsync(ct);

        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Shop)
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new InvalidOperationException("Produit introuvable.");

        if (!product.IsActive) throw new InvalidOperationException("Produit indisponible.");
        if (!product.Shop.IsVerified) throw new InvalidOperationException("Boutique non vérifiée.");
        if (product.StockQty < qty) throw new InvalidOperationException("Stock insuffisant.");
        var effectiveUnitPrice = product.GetEffectivePrice(DateTime.UtcNow);

        var existing = await _db.CartItems.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == productId, ct);
        if (existing is null)
        {
            _db.CartItems.Add(new Domain.Entities.CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                Qty = qty,
                UnitPrice = effectiveUnitPrice
            });
        }
        else
        {
            var newQty = existing.Qty + qty;
            if (product.StockQty < newQty) throw new InvalidOperationException("Stock insuffisant.");
            existing.Qty = newQty;
            existing.UnitPrice = effectiveUnitPrice; // refresh price snapshot (inclut la promo active)
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await MapCartAsync(cart.Id, ct);
    }

    public async Task<CartDto> UpdateItemAsync(Guid cartItemId, int qty, CancellationToken ct)
    {
        if (qty <= 0) throw new InvalidOperationException("Quantité invalide.");

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var cart = await GetOrCreateCartAsync(ct);

        var item = await _db.CartItems.FirstOrDefaultAsync(x => x.Id == cartItemId && x.CartId == cart.Id, ct)
                   ?? throw new InvalidOperationException("Item introuvable.");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId, ct)
                     ?? throw new InvalidOperationException("Produit introuvable.");

        if (product.StockQty < qty) throw new InvalidOperationException("Stock insuffisant.");
        var effectiveUnitPrice = product.GetEffectivePrice(DateTime.UtcNow);

        item.Qty = qty;
        item.UnitPrice = effectiveUnitPrice;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await MapCartAsync(cart.Id, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct)
    {
        var cart = await GetOrCreateCartAsync(ct);

        var item = await _db.CartItems.FirstOrDefaultAsync(x => x.Id == cartItemId && x.CartId == cart.Id, ct)
                   ?? throw new InvalidOperationException("Item introuvable.");

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        return await MapCartAsync(cart.Id, ct);
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        var cart = await GetOrCreateCartAsync(ct);

        var items = _db.CartItems.Where(x => x.CartId == cart.Id);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Domain.Entities.Cart> GetOrCreateCartAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is not null) return cart;

        cart = new Domain.Entities.Cart { UserId = userId };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return cart;
    }

    private async Task<CartDto> MapCartAsync(Guid cartId, CancellationToken ct)
    {
        var cartItems = await _db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .Include(i => i.Product)
            .ToListAsync(ct);

        var items = new List<CartItemDto>(cartItems.Count);
        foreach (var i in cartItems)
        {
            if (i.Product is null)
            {
                // Ignore orphan cart rows to prevent runtime null-reference crashes.
                continue;
            }

            items.Add(new CartItemDto(
                i.Id,
                i.ProductId,
                i.Product.ShopId,
                i.Product.Name,
                i.UnitPrice,
                i.Qty,
                i.UnitPrice * i.Qty,
                i.Product.Currency,
                i.Product.MainImageUrl
            ));
        }

        var currency = items.FirstOrDefault()?.Currency ?? "USD";
        var subtotal = items.Sum(x => x.LineTotal);

        return new CartDto(cartId, items, subtotal, currency);
    }
}
