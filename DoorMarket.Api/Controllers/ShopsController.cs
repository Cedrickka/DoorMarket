using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Application.Interfaces.Orders;
using DoorMarket.Application.Interfaces.Storage;
using DoorMarket.Api.Security;
using DoorMarket.Api.Utils;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DoorMarket.Application.Interfaces.Shops;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShopsController : ControllerBase
{
    private readonly IShopService _shops;
    private readonly IOrderService _orders;
    private readonly DoorMarketDbContext _db;
    private readonly IFileStorage _storage;

    public ShopsController(IShopService shops, IOrderService orders, DoorMarketDbContext db, IFileStorage storage)
    {
        _shops = shops;
        _orders = orders;
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShopDto>>> Search(
        [FromQuery] string? countryTag = null,
        [FromQuery] string? city = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] string? categoryId = null,
        [FromQuery] bool? recommended = null,
        CancellationToken ct = default)
    {
        var query = new ShopQuery(
            Normalize(countryTag),
            Normalize(city),
            Normalize(q),
            Math.Max(1, page),
            pageSize is < 1 or > 100 ? 20 : pageSize,
            verifiedOnly,
            ParseGuidOrNull(categoryId),
            recommended
        );

        return Ok(ToPublic(await _shops.SearchAsync(query, ct), Request));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShopDto>> Get(Guid id, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(id, ct);
        return shop is null ? NotFound() : Ok(ToPublic(shop, Request));
    }

    [Authorize]
    [HttpPost("mine")]
    public async Task<ActionResult<ShopDto>> CreateMine([FromBody] ShopCreateRequest req, CancellationToken ct)
        => Ok(ToPublic(await _shops.CreateMyShopAsync(req, ct), Request));

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<ShopDto?>> GetMine(CancellationToken ct)
    {
        var shop = await _shops.GetMyShopAsync(ct);
        return Ok(shop is null ? null : ToPublic(shop, Request));
    }

    [Authorize]
    [HttpPut("mine")]
    public async Task<ActionResult<ShopDto>> UpdateMine([FromBody] ShopUpdateRequest req, CancellationToken ct)
        => Ok(ToPublic(await _shops.UpdateMyShopAsync(req, ct), Request));

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpGet("admin")]
    public async Task<ActionResult<PagedResult<ShopDto>>> AdminSearch(
        [FromQuery] string? countryTag = null,
        [FromQuery] string? city = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool verifiedOnly = false,
        [FromQuery] string? categoryId = null,
        [FromQuery] bool? recommended = null,
        CancellationToken ct = default)
    {
        var query = new ShopQuery(
            Normalize(countryTag),
            Normalize(city),
            Normalize(q),
            Math.Max(1, page),
            pageSize is < 1 or > 100 ? 20 : pageSize,
            verifiedOnly,
            ParseGuidOrNull(categoryId),
            recommended
        );

        return Ok(ToPublic(await _shops.AdminSearchAsync(query, ct), Request));
    }

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpGet("admin/lookup")]
    public async Task<ActionResult<List<ShopLookupDto>>> AdminLookup([FromQuery] int limit = 200, CancellationToken ct = default)
    {
        limit = limit is < 1 or > 500 ? 200 : limit;
        var items = await _db.Shops.AsNoTracking()
            .OrderBy(s => s.Name)
            .Take(limit)
            .Select(s => new ShopLookupDto(s.Id, s.Name))
            .ToListAsync(ct);

        return Ok(items);
    }

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        await _shops.VerifyAsync(id, ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpPut("admin/{id:guid}")]
    public async Task<ActionResult<ShopDto>> AdminUpdate(Guid id, [FromBody] ShopUpdateRequest req, CancellationToken ct)
        => Ok(ToPublic(await _shops.AdminUpdateAsync(id, req, ct), Request));

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpPost("admin/{id:guid}/image")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AdminShopImageUploadResponse>> AdminUploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        var validated = await UploadSecurityValidator.ValidateImageAsync(file, ct);

        var shop = await _db.Shops.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Boutique introuvable.");

        var folder = $"shops/{shop.Id:N}/logo";
        await using var stream = file.OpenReadStream();
        var (_, url, sizeBytes) = await _storage.SaveAsync(stream, validated.FileName, validated.ContentType, folder, ct);
        var publicUrl = ResponseUrlNormalizer.ToAbsoluteUrl(url, Request);

        shop.ImageUrl = publicUrl ?? url;
        shop.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new AdminShopImageUploadResponse(shop.ImageUrl, validated.FileName, validated.ContentType, sizeBytes));
    }

    [Authorize(Roles = "Admin,SuperAdmin,3,4")]
    [HttpDelete("admin/{id:guid}")]
    public async Task<IActionResult> AdminDelete(Guid id, CancellationToken ct)
    {
        await _shops.AdminDeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("mine/summary")]
    public async Task<ActionResult<ShopDashboardSummaryDto>> MySummary(CancellationToken ct)
        => Ok(await _shops.GetMySummaryAsync(ct));

    [Authorize(Roles = "Shop,Admin,SuperAdmin,2,3,4")]
    [HttpPatch("orders/{orderId:guid}/fulfillment-status")]
    public async Task<IActionResult> PatchFulfillmentStatus(
        Guid orderId,
        [FromBody] ShopFulfillmentStatusUpdateRequest request,
        CancellationToken ct)
    {
        await _orders.SetFulfillmentStatusForMyShopAsync(orderId, request.Status, request.Note, ct);
        return NoContent();
    }

    public sealed record AdminShopImageUploadResponse(string Url, string FileName, string ContentType, long SizeBytes);
    public sealed record ShopLookupDto(Guid Id, string Name);

    private static ShopDto ToPublic(ShopDto dto, HttpRequest request)
        => dto with { ImageUrl = ResponseUrlNormalizer.ToAbsoluteUrl(dto.ImageUrl, request) };

    private static PagedResult<ShopDto> ToPublic(PagedResult<ShopDto> page, HttpRequest request)
        => new()
        {
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
            Items = page.Items.Select(x => ToPublic(x, request)).ToList()
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? ParseGuidOrNull(string? value)
        => Guid.TryParse(value, out var id) ? id : null;
}
