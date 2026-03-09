using DoorMarket.Application.Interfaces.Loyalty;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoorMarket.Infrastructure.Loyalty;

public class LoyaltyService : ILoyaltyService
{
    private readonly DoorMarketDbContext _db;
    private readonly ILogger<LoyaltyService> _logger;

    public LoyaltyService(DoorMarketDbContext db, ILogger<LoyaltyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AwardOrderPaidAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.UserId,
                o.TotalAmount,
                o.Currency,
                o.PaymentStatus
            })
            .FirstOrDefaultAsync(ct);

        if (order is null || !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceId = order.Id.ToString();
        var alreadyCredited = await _db.LoyaltyPointLedgers.AsNoTracking()
            .AnyAsync(x => x.SourceType == "OrderPaid" && x.SourceId == sourceId, ct);
        if (alreadyCredited)
        {
            return;
        }

        var rule = await _db.LoyaltyRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ThenByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var earnRate = rule?.EarnPointsPerUsd ?? 1m;
        var minOrderUsd = rule?.MinOrderAmountUsd ?? 0m;
        if (order.TotalAmount < minOrderUsd)
        {
            return;
        }

        var earnedPoints = (int)Math.Floor(order.TotalAmount * earnRate);
        if (earnedPoints <= 0)
        {
            return;
        }

        var wallet = await _db.LoyaltyWallets
            .FirstOrDefaultAsync(w => w.UserId == order.UserId, ct);
        if (wallet is null)
        {
            wallet = new LoyaltyWallet
            {
                UserId = order.UserId,
                Currency = string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency.Trim().ToUpperInvariant(),
                PointsBalance = 0,
                LifetimePointsEarned = 0,
                LifetimePointsSpent = 0,
                MonetaryBalance = 0m,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.LoyaltyWallets.Add(wallet);
            await _db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        wallet.PointsBalance += earnedPoints;
        wallet.LifetimePointsEarned += earnedPoints;
        wallet.LastActivityAtUtc = now;
        wallet.UpdatedAtUtc = now;

        _db.LoyaltyPointLedgers.Add(new LoyaltyPointLedger
        {
            WalletId = wallet.Id,
            UserId = order.UserId,
            EntryType = "Earn",
            DeltaPoints = earnedPoints,
            DeltaAmount = 0m,
            BalanceAfterPoints = wallet.PointsBalance,
            BalanceAfterAmount = wallet.MonetaryBalance,
            SourceType = "OrderPaid",
            SourceId = sourceId,
            Note = $"Points credited for order {order.Id:N}",
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Loyalty points credited. orderId={OrderId}, userId={UserId}, points={Points}", order.Id, order.UserId, earnedPoints);
    }
}
