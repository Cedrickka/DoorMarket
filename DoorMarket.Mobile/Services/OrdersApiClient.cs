using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public sealed class OrdersApiClient : ApiClientBase
{
    public OrdersApiClient(HttpClient http) : base(http)
    {
    }

    public Task<OrderDto> CreateAsync(CheckoutRequest request, CancellationToken ct)
        => PostAsync<OrderDto>("api/orders", request, ct);

    public Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
        => PostAsync<OrderDto>("api/orders/checkout", request, ct);

    public Task<PagedResult<OrderDto>> GetMineAsync(int page, int pageSize, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var path = QueryStringBuilder.AddQueryString("api/orders/mine", parameters);
        return GetAsync<PagedResult<OrderDto>>(path, ct);
    }

    public Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct)
        => GetAsync<OrderDto>($"api/orders/{id}", ct);
}
