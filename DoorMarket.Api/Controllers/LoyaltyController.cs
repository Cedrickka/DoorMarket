using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Loyalty;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/loyalty")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public LoyaltyController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("me")]
    public async Task<ActionResult<LoyaltyWalletDto>> GetMine(CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var wallet = await _db.LoyaltyWallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        if (wallet is null)
        {
            return Ok(new LoyaltyWalletDto(userId, 0, 0, 0, 0m, "USD", null));
        }

        return Ok(ToWalletDto(wallet));
    }

    [HttpGet("me/history")]
    public async Task<ActionResult<PagedResult<LoyaltyLedgerEntryDto>>> GetMyHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.LoyaltyPointLedgers.AsNoTracking().Where(x => x.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LoyaltyLedgerEntryDto(
                x.Id,
                x.EntryType,
                x.DeltaPoints,
                x.DeltaAmount,
                x.BalanceAfterPoints,
                x.BalanceAfterAmount,
                x.SourceType,
                x.SourceId,
                x.Note,
                x.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(new PagedResult<LoyaltyLedgerEntryDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        });
    }

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyRuleDto>>> GetRules(CancellationToken ct = default)
    {
        var rules = await _db.LoyaltyRules.AsNoTracking()
            .Where(r => r.IsActive)
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

        return Ok(rules);
    }

    private static LoyaltyWalletDto ToWalletDto(Domain.Entities.LoyaltyWallet wallet)
        => new(
            wallet.UserId,
            wallet.PointsBalance,
            wallet.LifetimePointsEarned,
            wallet.LifetimePointsSpent,
            wallet.MonetaryBalance,
            wallet.Currency,
            wallet.LastActivityAtUtc);
}
