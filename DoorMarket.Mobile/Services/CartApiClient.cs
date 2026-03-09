using DoorMarket.Application.DTOs.Cart;

namespace DoorMarket.Mobile.Services;

public sealed class CartApiClient : ApiClientBase
{
    public CartApiClient(HttpClient http) : base(http)
    {
    }

    public Task<CartDto> GetMyCartAsync(CancellationToken ct)
        => GetAsync<CartDto>("api/cart", ct);

    public Task<CartDto> AddItemAsync(Guid productId, int qty, CancellationToken ct)
        => PostAsync<CartDto>("api/cart/items", new AddCartItemRequest(productId, qty), ct);

    public Task<CartDto> UpdateItemAsync(Guid itemId, int qty, CancellationToken ct)
        => PutAsync<CartDto>($"api/cart/items/{itemId}", new UpdateCartItemRequest(qty), ct);

    public Task<CartDto> RemoveItemAsync(Guid itemId, CancellationToken ct)
        => DeleteAsync<CartDto>($"api/cart/items/{itemId}", ct);

    public Task ClearAsync(CancellationToken ct)
        => DeleteAsync("api/cart/clear", ct);

    public Task<PromoQuoteDto> ApplyPromoAsync(string code, CancellationToken ct)
        => PostAsync<PromoQuoteDto>("api/cart/apply-promo", new ApplyPromoRequest(code), ct);

    public Task<CheckoutReadinessDto> PreCheckoutAsync(PreCheckoutRequest request, CancellationToken ct)
        => PostAsync<CheckoutReadinessDto>("api/cart/pre-checkout", request, ct);
}
