using System.Globalization;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public sealed class ShopsApiClient : ApiClientBase
{
    public ShopsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<PagedResult<ShopDto>> SearchAsync(
        ShopQuery query,
        Guid? categoryId,
        bool? recommended,
        CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["countryTag"] = query.CountryTag,
            ["city"] = query.City,
            ["q"] = query.Q,
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
            ["verifiedOnly"] = query.VerifiedOnly.ToString().ToLowerInvariant(),
            ["categoryId"] = categoryId?.ToString(),
            ["recommended"] = recommended?.ToString()?.ToLowerInvariant()
        };

        var path = QueryStringBuilder.AddQueryString("api/shops", parameters);
        return GetAsync<PagedResult<ShopDto>>(path, ct);
    }

    public Task<ShopDto> GetByIdAsync(Guid id, CancellationToken ct)
        => GetAsync<ShopDto>($"api/shops/{id}", ct);
}
