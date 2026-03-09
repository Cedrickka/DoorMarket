using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminOrdersController : ControllerBase
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;

    public AdminOrdersController(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOrdersPaged>> GetOrders(
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Orders.AsNoTracking()
            .Include(o => o.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = query.Where(o =>
                o.FulfillmentStatus == s ||
                o.PaymentStatus == s ||
                o.Status == s);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToUniversalTime();
            query = query.Where(o => o.CreatedAtUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            if (toUtc.TimeOfDay == TimeSpan.Zero)
            {
                toUtc = toUtc.AddDays(1);
            }
            query = query.Where(o => o.CreatedAtUtc < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(o =>
                o.DeliveryName.Contains(s) ||
                o.DeliveryPhone.Contains(s) ||
                (o.User != null && o.User.Email.Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.DeliveryName,
                o.DeliveryPhone,
                UserEmail = o.User != null ? o.User.Email : null,
                o.Status,
                o.PaymentStatus,
                o.FulfillmentStatus,
                o.PaidAtUtc,
                o.CreatedAtUtc,
                o.TotalAmount,
                o.Subtotal,
                o.TotalItemsAmount,
                o.DeliveryFee,
                o.PlatformFeeTotal,
                o.Currency
            })
            .ToListAsync(ct);

        var ids = orders.Select(x => x.Id).ToList();
        var shopsCountMap = await _db.OrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.Product.ShopId })
            .Distinct()
            .GroupBy(x => x.OrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count, ct);

        var platformFeeMap = await _db.OrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.OrderId))
            .GroupBy(i => i.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                PlatformFeeTotal = g.Sum(x => x.PlatformFeeAtPurchase * x.Qty)
            })
            .ToDictionaryAsync(x => x.OrderId, x => x.PlatformFeeTotal, ct);

        var rows = orders.Select(o =>
        {
            var platformFee = o.PlatformFeeTotal > 0m ? o.PlatformFeeTotal :
                (platformFeeMap.TryGetValue(o.Id, out var pf) ? pf : 0m);
            var itemsAmount = o.TotalItemsAmount > 0m ? o.TotalItemsAmount : o.Subtotal;
            return new AdminOrderRow(
                o.Id,
                BuildOrderCode(o.Id),
                o.DeliveryName,
                o.UserEmail,
                o.Status,
                o.PaymentStatus,
                o.FulfillmentStatus,
                o.PaidAtUtc,
                itemsAmount,
                o.DeliveryFee,
                o.TotalAmount,
                o.Currency,
                platformFee,
                shopsCountMap.TryGetValue(o.Id, out var sc) ? sc : 0
            );
        }).ToList();

        return Ok(new AdminOrdersPaged(rows, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDetail>> GetOrder(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null) return NotFound();

        var items = await _db.OrderItems.AsNoTracking()
            .Include(i => i.Product)
            .Where(i => i.OrderId == id)
            .Select(i => new AdminOrderItemDetail(
                i.ProductId,
                i.Product.Name,
                i.Qty,
                i.UnitPriceAtPurchase != 0m ? i.UnitPriceAtPurchase : i.UnitPrice,
                i.PlatformFeeAtPurchase,
                (i.UnitPriceAtPurchase != 0m ? i.UnitPriceAtPurchase : i.UnitPrice) * i.Qty,
                i.PlatformFeeAtPurchase * i.Qty,
                i.Product.Currency
            ))
            .ToListAsync(ct);

        var platformFeeTotal = order.PlatformFeeTotal;
        if (platformFeeTotal <= 0m)
        {
            platformFeeTotal = items.Sum(x => x.PlatformFeeLine);
        }

        var detail = new AdminOrderDetail(
            order.Id,
            BuildOrderCode(order.Id),
            order.DeliveryName,
            order.DeliveryPhone,
            order.DeliveryLine1,
            order.DeliveryCity,
            order.DeliveryCountry,
            order.Status,
            order.PaymentStatus,
            order.FulfillmentStatus,
            order.PaidAtUtc,
            order.DeliveredAtUtc,
            order.Subtotal,
            order.TotalItemsAmount > 0m ? order.TotalItemsAmount : order.Subtotal,
            order.DeliveryFee,
            order.Discount,
            order.TotalAmount,
            platformFeeTotal,
            order.Currency,
            order.PaymentProvider,
            order.User?.Email,
            items
        );

        return Ok(detail);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminOrderStatusUpdateRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Status))
        {
            return BadRequest("Status requis.");
        }

        var status = req.Status.Trim();
        if (!string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Seul le statut Delivered est autorise.");
        }

        var order = await _db.Orders.AsTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        if (string.Equals(order.FulfillmentStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            return NoContent();
        }

        if (!string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Commande non payee.");
        }

        var previous = order.FulfillmentStatus;
        order.FulfillmentStatus = "Delivered";
        order.Status = "Delivered";
        order.DeliveredAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;

        _db.OrderStatusHistories.Add(new Domain.Entities.OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = previous,
            NewStatus = order.FulfillmentStatus,
            ChangedByUserId = _current.UserId,
            ChangedAtUtc = DateTime.UtcNow,
            Note = "Marque comme livree par admin."
        });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string BuildOrderCode(Guid orderId)
        => $"DM{orderId.ToString("N")[..6].ToUpperInvariant()}";

    public sealed record AdminOrderStatusUpdateRequest(string Status);

    public sealed record AdminOrderRow(
        Guid OrderId,
        string OrderNumber,
        string CustomerName,
        string? CustomerEmail,
        string Status,
        string PaymentStatus,
        string FulfillmentStatus,
        DateTime? PaidAtUtc,
        decimal TotalItemsAmount,
        decimal DeliveryFee,
        decimal GrandTotal,
        string Currency,
        decimal PlatformFeeTotal,
        int ShopsCount);

    public sealed record AdminOrdersPaged(List<AdminOrderRow> Items, int Total, int Page, int PageSize);

    public sealed record AdminOrderItemDetail(
        Guid ProductId,
        string ProductName,
        int Qty,
        decimal UnitPriceAtPurchase,
        decimal PlatformFeeAtPurchase,
        decimal LineTotal,
        decimal PlatformFeeLine,
        string Currency);

    public sealed record AdminOrderDetail(
        Guid OrderId,
        string OrderNumber,
        string CustomerName,
        string CustomerPhone,
        string DeliveryLine1,
        string DeliveryCity,
        string DeliveryCountry,
        string Status,
        string PaymentStatus,
        string FulfillmentStatus,
        DateTime? PaidAtUtc,
        DateTime? DeliveredAtUtc,
        decimal Subtotal,
        decimal TotalItemsAmount,
        decimal DeliveryFee,
        decimal Discount,
        decimal GrandTotal,
        decimal PlatformFeeTotal,
        string Currency,
        string PaymentProvider,
        string? CustomerEmail,
        IReadOnlyList<AdminOrderItemDetail> Items);
}
