using System.Globalization;
using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.DTOs.Delivery;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public sealed class DeliveryApiClient : ApiClientBase
{
    public DeliveryApiClient(HttpClient http) : base(http)
    {
    }

    public Task<DeliveryQuoteDto> GetQuoteAsync(decimal subtotal, string? currency, Guid? zoneId, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["subtotal"] = subtotal.ToString(CultureInfo.InvariantCulture),
            ["currency"] = currency,
            ["zoneId"] = zoneId?.ToString()
        };

        var path = QueryStringBuilder.AddQueryString("api/delivery/quote", parameters);
        return GetAsync<DeliveryQuoteDto>(path, ct);
    }

    public Task<List<DeliveryZoneDto>> GetZonesAsync(CancellationToken ct)
        => GetAsync<List<DeliveryZoneDto>>("api/delivery/zones", ct);
}
