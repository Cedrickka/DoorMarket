using DoorMarket.Application.DTOs.Loyalty;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/loyalty")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminLoyaltyController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminLoyaltyController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyRuleDto>>> GetRules(
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
    {
        var query = _db.LoyaltyRules.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var rows = await query
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name)
            .Select(r => new LoyaltyRuleDto(
                r.Id,
                r.Name,
                r.IsActive,
                r.Priority,
                r.EarnPointsPerUsd,
                r.MinOrderAmountUsd,
                r.RedeemValueUsdPerPoint,
                r.MinRedeemPoints,
                r.MaxRedeemPercentOfOrder,
                r.Notes,
                r.CreatedAtUtc,
                r.UpdatedAtUtc))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost("rules")]
    public async Task<ActionResult<LoyaltyRuleDto>> UpsertRule(
        [FromBody] UpsertLoyaltyRuleRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Name requis.");
        }

        if (req.EarnPointsPerUsd <= 0m)
        {
            return BadRequest("EarnPointsPerUsd doit etre > 0.");
        }

        if (req.MinOrderAmountUsd.HasValue && req.MinOrderAmountUsd.Value < 0m)
        {
            return BadRequest("MinOrderAmountUsd doit etre >= 0.");
        }

        if (req.RedeemValueUsdPerPoint <= 0m)
        {
            return BadRequest("RedeemValueUsdPerPoint doit etre > 0.");
        }

        if (req.MinRedeemPoints < 0)
        {
            return BadRequest("MinRedeemPoints doit etre >= 0.");
        }

        if (req.MaxRedeemPercentOfOrder is < 0m or > 100m)
        {
            return BadRequest("MaxRedeemPercentOfOrder doit etre entre 0 et 100.");
        }

        var now = DateTime.UtcNow;
        Domain.Entities.LoyaltyRule entity;
        if (req.Id.HasValue && req.Id.Value != Guid.Empty)
        {
            entity = await _db.LoyaltyRules.FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                ?? new Domain.Entities.LoyaltyRule { Id = req.Id.Value, CreatedAtUtc = now };
            if (entity.CreatedAtUtc == default)
            {
                entity.CreatedAtUtc = now;
            }
            if (_db.Entry(entity).State == EntityState.Detached)
            {
                _db.LoyaltyRules.Add(entity);
            }
        }
        else
        {
            entity = new Domain.Entities.LoyaltyRule { CreatedAtUtc = now };
            _db.LoyaltyRules.Add(entity);
        }

        entity.Name = req.Name.Trim();
        entity.IsActive = req.IsActive;
        entity.Priority = req.Priority;
        entity.EarnPointsPerUsd = decimal.Round(req.EarnPointsPerUsd, 4, MidpointRounding.AwayFromZero);
        entity.MinOrderAmountUsd = req.MinOrderAmountUsd.HasValue
            ? decimal.Round(req.MinOrderAmountUsd.Value, 2, MidpointRounding.AwayFromZero)
            : null;
        entity.RedeemValueUsdPerPoint = decimal.Round(req.RedeemValueUsdPerPoint, 6, MidpointRounding.AwayFromZero);
        entity.MinRedeemPoints = req.MinRedeemPoints;
        entity.MaxRedeemPercentOfOrder = decimal.Round(req.MaxRedeemPercentOfOrder, 2, MidpointRounding.AwayFromZero);
        entity.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        return Ok(new LoyaltyRuleDto(
            entity.Id,
            entity.Name,
            entity.IsActive,
            entity.Priority,
            entity.EarnPointsPerUsd,
            entity.MinOrderAmountUsd,
            entity.RedeemValueUsdPerPoint,
            entity.MinRedeemPoints,
            entity.MaxRedeemPercentOfOrder,
            entity.Notes,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc));
    }

    public sealed record UpsertLoyaltyRuleRequest(
        Guid? Id,
        string Name,
        bool IsActive,
        int Priority,
        decimal EarnPointsPerUsd,
        decimal? MinOrderAmountUsd,
        decimal RedeemValueUsdPerPoint,
        int MinRedeemPoints,
        decimal MaxRedeemPercentOfOrder,
        string? Notes);
}
