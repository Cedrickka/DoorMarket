using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.DTOs.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoorMarket.Api.Utils;

internal static class ResponseUrlNormalizer
{
    public static ProductDto ToPublicUrls(this ProductDto dto, HttpRequest request)
        => dto with { MainImageUrl = ToAbsoluteUrl(dto.MainImageUrl, request) };

    public static PagedResult<ProductDto> ToPublicUrls(this PagedResult<ProductDto> page, HttpRequest request)
        => new()
        {
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
            Items = page.Items.Select(p => p.ToPublicUrls(request)).ToList()
        };

    public static CartDto ToPublicUrls(this CartDto cart, HttpRequest request)
        => cart with
        {
            Items = cart.Items
                .Select(i => i with { MainImageUrl = ToAbsoluteUrl(i.MainImageUrl, request) })
                .ToList()
        };

    public static string? ToAbsoluteUrl(string? url, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var config = request.HttpContext.RequestServices.GetService<IConfiguration>();
        var publicBaseUrl = config?["Uploads:PublicBaseUrl"]
                            ?? config?["PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBaseUrl) && Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var existingAbsolute))
            {
                if (string.Equals(existingAbsolute.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new UriBuilder(existingAbsolute)
                    {
                        Scheme = baseUri.Scheme,
                        Host = baseUri.Host,
                        Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
                    };
                    return builder.Uri.ToString();
                }

                return existingAbsolute.ToString();
            }

            var normalizedRelative = url.StartsWith("/", StringComparison.Ordinal) ? url : "/" + url;
            return new Uri(baseUri, normalizedRelative).ToString();
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        var normalized = url.StartsWith("/", StringComparison.Ordinal) ? url : "/" + url;
        return $"{request.Scheme}://{request.Host}{normalized}";
    }
}
