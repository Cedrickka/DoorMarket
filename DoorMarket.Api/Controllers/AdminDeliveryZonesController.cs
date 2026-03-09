using DoorMarket.Application.DTOs.Delivery;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/delivery-zones")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public sealed class AdminDeliveryZonesController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminDeliveryZonesController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeliveryZoneDto>>> GetAll(CancellationToken ct)
    {
        var zones = await _db.DeliveryZones.AsNoTracking()
            .OrderBy(z => z.Country)
            .ThenBy(z => z.StateCode)
            .ThenBy(z => z.Name)
            .Select(z => new DeliveryZoneDto(z.Id, z.Code, z.Name, z.Country, z.StateCode, z.FeeUsd, z.IsActive))
            .ToListAsync(ct);

        return Ok(zones);
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryZoneDto>> Create([FromBody] UpsertDeliveryZoneRequest req, CancellationToken ct)
    {
        var code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Code zone obligatoire.");
        }

        if (req.FeeUsd < 0m)
        {
            throw new InvalidOperationException("Tarif de zone invalide.");
        }

        var exists = await _db.DeliveryZones.AnyAsync(z => z.Code == code, ct);
        if (exists)
        {
            throw new InvalidOperationException("Code zone deja utilise.");
        }

        var zone = new Domain.Entities.DeliveryZone
        {
            Code = code,
            Name = req.Name.Trim(),
            Country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country.Trim().ToUpperInvariant(),
            StateCode = string.IsNullOrWhiteSpace(req.StateCode) ? null : req.StateCode.Trim().ToUpperInvariant(),
            FeeUsd = decimal.Round(req.FeeUsd, 2),
            IsActive = req.IsActive
        };

        _db.DeliveryZones.Add(zone);
        await _db.SaveChangesAsync(ct);

        return Ok(new DeliveryZoneDto(zone.Id, zone.Code, zone.Name, zone.Country, zone.StateCode, zone.FeeUsd, zone.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryZoneDto>> Update(Guid id, [FromBody] UpsertDeliveryZoneRequest req, CancellationToken ct)
    {
        var zone = await _db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id, ct)
            ?? throw new InvalidOperationException("Zone introuvable.");

        var code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Code zone obligatoire.");
        }

        if (req.FeeUsd < 0m)
        {
            throw new InvalidOperationException("Tarif de zone invalide.");
        }

        var duplicate = await _db.DeliveryZones.AnyAsync(z => z.Id != id && z.Code == code, ct);
        if (duplicate)
        {
            throw new InvalidOperationException("Code zone deja utilise.");
        }

        zone.Code = code;
        zone.Name = req.Name.Trim();
        zone.Country = string.IsNullOrWhiteSpace(req.Country) ? "US" : req.Country.Trim().ToUpperInvariant();
        zone.StateCode = string.IsNullOrWhiteSpace(req.StateCode) ? null : req.StateCode.Trim().ToUpperInvariant();
        zone.FeeUsd = decimal.Round(req.FeeUsd, 2);
        zone.IsActive = req.IsActive;
        zone.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new DeliveryZoneDto(zone.Id, zone.Code, zone.Name, zone.Country, zone.StateCode, zone.FeeUsd, zone.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var zone = await _db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id, ct)
            ?? throw new InvalidOperationException("Zone introuvable.");

        var inUse = await _db.UserAddresses.AnyAsync(a => a.DeliveryZoneId == id, ct);
        if (inUse)
        {
            zone.IsActive = false;
            zone.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        _db.DeliveryZones.Remove(zone);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
