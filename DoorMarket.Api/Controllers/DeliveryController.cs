using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.DTOs.Delivery;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/delivery")]
[Authorize]
public class DeliveryController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public DeliveryController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("quote")]
    public async Task<ActionResult<DeliveryQuoteDto>> Quote(
        [FromQuery] decimal subtotal = 0m,
        [FromQuery] string? currency = null,
        [FromQuery] Guid? zoneId = null,
        CancellationToken ct = default)
    {
        if (subtotal < 0m)
        {
            subtotal = 0m;
        }

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "USD"
            : currency.Trim().ToUpperInvariant();

        if (subtotal <= 0m)
        {
            return Ok(new DeliveryQuoteDto(subtotal, 0m, normalizedCurrency));
        }

        if (!zoneId.HasValue)
        {
            return BadRequest("Zone de livraison obligatoire.");
        }

        var zoneFee = await _db.DeliveryZones.AsNoTracking()
            .Where(z => z.Id == zoneId.Value && z.IsActive)
            .Select(z => (decimal?)z.FeeUsd)
            .FirstOrDefaultAsync(ct);

        if (!zoneFee.HasValue)
        {
            return BadRequest("Zone de livraison invalide ou inactive.");
        }

        return Ok(new DeliveryQuoteDto(subtotal, decimal.Round(zoneFee.Value, 2), normalizedCurrency));
    }

    [HttpGet("zones")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DeliveryZoneDto>>> Zones(CancellationToken ct = default)
    {
        var zones = await _db.DeliveryZones.AsNoTracking()
            .Where(z => z.IsActive)
            .OrderBy(z => z.Country)
            .ThenBy(z => z.StateCode)
            .ThenBy(z => z.Name)
            .Select(z => new DeliveryZoneDto(z.Id, z.Code, z.Name, z.Country, z.StateCode, z.FeeUsd, z.IsActive))
            .ToListAsync(ct);

        return Ok(zones);
    }
}
