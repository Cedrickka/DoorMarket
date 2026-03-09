using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.ShopOnboarding;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/shop-applications")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminShopApplicationsController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminShopApplicationsController(DoorMarketDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShopApplicationDto>>> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.ShopApplications.AsNoTracking()
            .Include(x => x.Documents)
            .OrderByDescending(x => x.CreatedAtUtc)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status.Trim());

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new PagedResult<ShopApplicationDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items.Select(a => new ShopApplicationDto(
                a.Id, a.Status, a.Name, a.ImageUrl, a.CountryTag, a.City, a.ReviewNote, a.CreatedAtUtc,
                a.Documents.OrderByDescending(d => d.CreatedAtUtc)
                    .Select(d => new ShopApplicationDocumentDto(d.Id, d.DocType, d.FileName, d.PublicUrl, d.SizeBytes, d.CreatedAtUtc))
                    .ToList()
            )).ToList()
        });
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, AdminReviewShopApplicationRequest req, CancellationToken ct)
    {
        var app = await _db.ShopApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Candidature introuvable.");

        if (app.Status != "Submitted")
            throw new InvalidOperationException("Seules les candidatures Submitted peuvent être traitées.");

        var action = req.Action?.Trim();
        if (action is not ("Approve" or "Reject"))
            throw new InvalidOperationException("Action invalide (Approve/Reject).");

        app.ReviewNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        app.ReviewedAtUtc = DateTime.UtcNow;

        if (action == "Reject")
        {
            app.Status = "Rejected";
            app.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // Approve: créer Shop + IsVerified=true + role Shop
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == app.OwnerUserId, ct)
                   ?? throw new InvalidOperationException("User introuvable.");

        var alreadyHasShop = await _db.Shops.AnyAsync(s => s.OwnerUserId == app.OwnerUserId, ct);
        if (alreadyHasShop) throw new InvalidOperationException("Cet utilisateur a déjà une boutique.");

        var shop = new Domain.Entities.Shop
        {
            OwnerUserId = user.Id,
            Name = app.Name,
            ImageUrl = app.ImageUrl,
            CountryTag = app.CountryTag,
            City = app.City,
            IsVerified = true
        };

        user.Role = UserRole.Shop;

        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);

        app.Status = "Approved";
        app.ShopId = shop.Id;
        app.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
