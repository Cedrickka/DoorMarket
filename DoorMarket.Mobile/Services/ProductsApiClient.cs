using System.Globalization;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Mobile.Http;

namespace DoorMarket.Mobile.Services;

public sealed class ProductsApiClient : ApiClientBase
{
    public ProductsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<PagedResult<ProductDto>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["shopId"] = query.ShopId?.ToString(),
            ["categoryId"] = query.CategoryId?.ToString(),
            ["countryTag"] = query.CountryTag,
            ["city"] = query.City,
            ["q"] = query.Q,
            ["minPrice"] = query.MinPrice?.ToString(CultureInfo.InvariantCulture),
            ["maxPrice"] = query.MaxPrice?.ToString(CultureInfo.InvariantCulture),
            ["inStockOnly"] = query.InStockOnly.ToString().ToLowerInvariant(),
            ["activeOnly"] = query.ActiveOnly.ToString().ToLowerInvariant(),
            ["promotedOnly"] = query.PromotedOnly.ToString().ToLowerInvariant(),
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        };

        var path = QueryStringBuilder.AddQueryString("api/products", parameters);
        return GetAsync<PagedResult<ProductDto>>(path, ct);
    }

    public Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct)
        => GetAsync<ProductDto>($"api/products/{id}", ct);
}
