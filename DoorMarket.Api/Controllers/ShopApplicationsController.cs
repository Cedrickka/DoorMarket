using DoorMarket.Application.DTOs.ShopOnboarding;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Storage;
using DoorMarket.Api.Security;
using DoorMarket.Api.Utils;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/shop-applications")]
[Authorize]
public class ShopApplicationsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFileStorage _storage;

    public ShopApplicationsController(DoorMarketDbContext db, ICurrentUserService current, IFileStorage storage)
    {
        _db = db;
        _current = current;
        _storage = storage;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<ShopApplicationDto>> Mine(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var app = await _db.ShopApplications.AsNoTracking()
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);

        return app is null ? NotFound() : Ok(Map(app));
    }

    [HttpPost]
    public async Task<ActionResult<ShopApplicationDto>> CreateOrUpdate(CreateShopApplicationRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var hasShop = await _db.Shops.AsNoTracking().AnyAsync(s => s.OwnerUserId == userId, ct);
        if (hasShop) throw new InvalidOperationException("Vous avez deja une boutique.");

        var app = await _db.ShopApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);

        if (app is null)
        {
            app = new Domain.Entities.ShopApplication { OwnerUserId = userId, Status = "Draft" };
            _db.ShopApplications.Add(app);
        }
        else if (app.Status is "Submitted" or "Approved")
        {
            throw new InvalidOperationException("Candidature deja soumise.");
        }

        app.Name = req.Name.Trim();
        if (!string.IsNullOrWhiteSpace(req.ImageUrl))
        {
            app.ImageUrl = req.ImageUrl.Trim();
        }

        app.CountryTag = req.CountryTag.Trim().ToUpperInvariant();
        app.City = req.City.Trim();
        app.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(app));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var app = await _db.ShopApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Candidature introuvable.");

        if (app.Status != "Draft") throw new InvalidOperationException("Statut invalide.");
        if (app.Documents.Count == 0) throw new InvalidOperationException("Ajoute au moins un document.");

        app.Status = "Submitted";
        app.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ShopApplicationDocumentDto>> Upload(Guid id, [FromForm] string docType, IFormFile file, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var app = await _db.ShopApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Candidature introuvable.");

        if (app.Status is "Submitted" or "Approved")
            throw new InvalidOperationException("Upload interdit apres soumission.");

        var validated = await UploadSecurityValidator.ValidateDocumentAsync(file, ct);

        var folder = $"shop-applications/{app.Id:N}";
        await using var stream = file.OpenReadStream();
        var (path, url, size) = await _storage.SaveAsync(stream, validated.FileName, validated.ContentType, folder, ct);

        var doc = new Domain.Entities.ShopApplicationDocument
        {
            ShopApplicationId = app.Id,
            DocType = docType.Trim().ToUpperInvariant(),
            FileName = validated.FileName,
            ContentType = validated.ContentType,
            SizeBytes = size,
            StoragePath = path,
            PublicUrl = url
        };

        _db.ShopApplicationDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        return Ok(new ShopApplicationDocumentDto(doc.Id, doc.DocType, doc.FileName, doc.PublicUrl, doc.SizeBytes, doc.CreatedAtUtc));
    }

    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ShopImageUploadResponse>> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");

        var app = await _db.ShopApplications
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Candidature introuvable.");

        if (app.Status == "Submitted")
            throw new InvalidOperationException("Upload interdit apres soumission.");

        var validated = await UploadSecurityValidator.ValidateImageAsync(file, ct);

        var folder = $"shop-applications/{app.Id:N}/logo";
        await using var stream = file.OpenReadStream();
        var (_, url, sizeBytes) = await _storage.SaveAsync(stream, validated.FileName, validated.ContentType, folder, ct);
        var publicUrl = ResponseUrlNormalizer.ToAbsoluteUrl(url, Request);

        var finalUrl = publicUrl ?? url;
        app.ImageUrl = finalUrl;
        app.UpdatedAtUtc = DateTime.UtcNow;

        if (app.Status == "Approved" && app.ShopId.HasValue)
        {
            var shop = await _db.Shops.FirstOrDefaultAsync(x => x.Id == app.ShopId.Value, ct);
            if (shop is not null)
            {
                shop.ImageUrl = finalUrl;
                shop.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new ShopImageUploadResponse(finalUrl, validated.FileName, validated.ContentType, sizeBytes));
    }

    private static ShopApplicationDto Map(Domain.Entities.ShopApplication a)
        => new(
            a.Id,
            a.Status,
            a.Name,
            a.ImageUrl,
            a.CountryTag,
            a.City,
            a.ReviewNote,
            a.CreatedAtUtc,
            a.Documents
                .OrderByDescending(d => d.CreatedAtUtc)
                .Select(d => new ShopApplicationDocumentDto(d.Id, d.DocType, d.FileName, d.PublicUrl, d.SizeBytes, d.CreatedAtUtc))
                .ToList()
        );

    public sealed record ShopImageUploadResponse(string Url, string FileName, string ContentType, long SizeBytes);
}
