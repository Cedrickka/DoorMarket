using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/bank")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminBankController : ControllerBase
{
    private readonly DoorMarketDbContext _db;

    public AdminBankController(DoorMarketDbContext db)
    {
        _db = db;
    }

    [HttpPost("balance-snapshot")]
    public async Task<ActionResult<BankBalanceSnapshotDto>> CreateSnapshot([FromBody] BankBalanceSnapshotRequest req, CancellationToken ct)
    {
        if (req.AsOfDateUtc == default)
            return BadRequest("AsOfDate requis.");

        var snap = new Domain.Entities.BankBalanceSnapshot
        {
            AsOfDateUtc = req.AsOfDateUtc.ToUniversalTime(),
            Balance = req.Balance,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()
        };

        _db.BankBalanceSnapshots.Add(snap);
        await _db.SaveChangesAsync(ct);

        return Ok(new BankBalanceSnapshotDto(snap.Id, snap.AsOfDateUtc, snap.Balance, snap.Note, snap.CreatedAtUtc));
    }

    [HttpGet("balance-snapshot/latest")]
    public async Task<ActionResult<BankBalanceSnapshotDto?>> Latest(CancellationToken ct)
    {
        var snap = await _db.BankBalanceSnapshots.AsNoTracking()
            .OrderByDescending(x => x.AsOfDateUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (snap is null) return Ok(null);
        return Ok(new BankBalanceSnapshotDto(snap.Id, snap.AsOfDateUtc, snap.Balance, snap.Note, snap.CreatedAtUtc));
    }

    public sealed record BankBalanceSnapshotRequest(DateTime AsOfDateUtc, decimal Balance, string? Note);

    public sealed record BankBalanceSnapshotDto(
        Guid Id,
        DateTime AsOfDateUtc,
        decimal Balance,
        string? Note,
        DateTime CreatedAtUtc);
}
