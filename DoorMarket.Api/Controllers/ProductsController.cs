using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Products;
using DoorMarket.Application.Interfaces.Products;
using DoorMarket.Application.Interfaces.Storage;
using DoorMarket.Api.Security;
using DoorMarket.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IFileStorage _storage;

    public ProductsController(IProductService products, IFileStorage storage)
    {
        _products = products;
        _storage = storage;
    }

    // PUBLIC: search/list
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search(
        [FromQuery] string? shopId = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? countryTag = null,
        [FromQuery] string? city = null,
        [FromQuery] string? q = null,
        [FromQuery] string? minPrice = null,
        [FromQuery] string? maxPrice = null,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] bool activeOnly = true,
        [FromQuery] bool promotedOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ProductQuery(
            ParseGuidOrNull(shopId),
            ParseGuidOrNull(categoryId),
            Normalize(countryTag),
            Normalize(city),
            Normalize(q),
            ParseDecimalOrNull(minPrice),
            ParseDecimalOrNull(maxPrice),
            inStockOnly,
            activeOnly,
            promotedOnly,
            Math.Max(1, page),
            pageSize is < 1 or > 100 ? 20 : pageSize
        );

        return Ok((await _products.SearchAsync(query, ct)).ToPublicUrls(Request));
    }

    // PUBLIC: detail
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(id, ct);
        return p is null ? NotFound() : Ok(p.ToPublicUrls(Request));
    }

    // OWNER: list my products
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ProductDto>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok((await _products.GetMyProductsAsync(page, pageSize, ct)).ToPublicUrls(Request));

    // OWNER: create
    [Authorize]
    [HttpPost("mine")]
    public async Task<ActionResult<ProductDto>> CreateMine([FromBody] ProductCreateRequest req, CancellationToken ct)
        => Ok((await _products.CreateMyProductAsync(req, ct)).ToPublicUrls(Request));

    // OWNER: update
    [Authorize]
    [HttpPut("mine/{id:guid}")]
    public async Task<ActionResult<ProductDto>> UpdateMine(Guid id, [FromBody] ProductUpdateRequest req, CancellationToken ct)
        => Ok((await _products.UpdateMyProductAsync(id, req, ct)).ToPublicUrls(Request));

    // OWNER: stock
    [Authorize]
    [HttpPatch("mine/{id:guid}/stock")]
    public async Task<IActionResult> Stock(Guid id, [FromQuery] int qty, CancellationToken ct)
    {
        await _products.AdjustStockAsync(id, qty, ct);
        return NoContent();
    }

    // OWNER/ADMIN: activate/deactivate
    [Authorize]
    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> Active(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        await _products.SetActiveAsync(id, value, ct);
        return NoContent();
    }

    // OWNER/ADMIN: upload image and get a public URL
    [Authorize]
    [HttpPost("upload-image")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ProductImageUploadResponse>> UploadImage(IFormFile file, CancellationToken ct)
    {
        var validated = await UploadSecurityValidator.ValidateImageAsync(file, ct);

        var folder = $"products/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        await using var stream = file.OpenReadStream();
        var (_, url, sizeBytes) = await _storage.SaveAsync(stream, validated.FileName, validated.ContentType, folder, ct);
        var publicUrl = ResponseUrlNormalizer.ToAbsoluteUrl(url, Request);

        return Ok(new ProductImageUploadResponse(publicUrl ?? url, validated.FileName, validated.ContentType, sizeBytes));
    }

    public sealed record ProductImageUploadResponse(string Url, string FileName, string ContentType, long SizeBytes);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? ParseGuidOrNull(string? value)
        => Guid.TryParse(value, out var id) ? id : null;

    private static decimal? ParseDecimalOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)
            ? parsed
            : null;
    }
}
