using DoorMarket.Api.Services;
using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Application.DTOs.Shops;
using DoorMarket.Application.Interfaces.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;


namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IClientOrderNotificationService _clientNotifications;
    private readonly ICheckoutObservabilityService _checkoutObservability;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orders,
        IClientOrderNotificationService clientNotifications,
        ICheckoutObservabilityService checkoutObservability,
        ILogger<OrdersController> logger)
    {
        _orders = orders;
        _clientNotifications = clientNotifications;
        _checkoutObservability = checkoutObservability;
        _logger = logger;
    }

    // Client checkout
    [HttpPost]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<OrderDto>> Create(CheckoutRequest req, CancellationToken ct)
        => Ok(await CheckoutInternalAsync(req, ct));

    // Client checkout
    [HttpPost("checkout")]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest req, CancellationToken ct)
        => Ok(await CheckoutInternalAsync(req, ct));

    // Client my orders
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<OrderDto>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _orders.GetMyOrdersAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetMyOrderByIdAsync(id, ct);
        return o is null ? NotFound() : Ok(o);
    }
    [Authorize(Roles = "Shop,2")]
    [HttpGet("shop/mine")]
    public async Task<ActionResult<PagedResult<OrderDto>>> MyShopOrders(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _orders.GetMyShopOrdersAsync(status, page, pageSize, ct));

    [Authorize(Roles = "Shop,2")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromQuery] string value, CancellationToken ct)
    {
        await _orders.SetStatusForMyShopAsync(id, value, ct);
        return NoContent();
    }

    [Authorize(Roles = "Shop,2")]
    [HttpGet("shop/{id:guid}")]
    public async Task<ActionResult<ShopOrderDetailDto>> ShopOrder(Guid id, CancellationToken ct)
    {
        var dto = await _orders.GetMyShopOrderByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = "Shop,2")]
    [HttpGet("shop/summary")]
    public async Task<ActionResult<ShopDashboardSummaryDto>> ShopSummary(CancellationToken ct)
        => Ok(await _orders.GetMyShopSummaryAsync(ct));

    // Shop endpoints (owner)

    private async Task<OrderDto> CheckoutInternalAsync(CheckoutRequest req, CancellationToken ct)
    {
        var order = await _orders.CheckoutAsync(req, ct);

        try
        {
            await _checkoutObservability.TrackAsync(
                eventType: CheckoutObservabilityEvents.OrderCreated,
                orderId: order.Id,
                paymentProvider: req.PaymentProvider,
                paymentChannel: ResolvePaymentChannel(req.PaymentProvider),
                success: true,
                source: "Api",
                metadata: new
                {
                    orderId = order.Id,
                    orderTotal = order.TotalAmount,
                    orderCurrency = order.Currency
                },
                ct: ct);
        }
        catch
        {
            // Do not block checkout flow on observability issues.
        }

        try
        {
            await _clientNotifications.NotifyClientOrderCreatedAsync(order.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client order-created notification failed. orderId={OrderId}", order.Id);
        }

        return order;
    }

    private static string ResolvePaymentChannel(string? provider)
    {
        var normalized = (provider ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Unknown";
        }

        if (normalized.Equals("Stripe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
        {
            return "External";
        }

        if (normalized.Equals("MobileMoney", StringComparison.OrdinalIgnoreCase))
        {
            return "MobileMoney";
        }

        if (normalized.Equals("PrepaidCard", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PrepaidInternal", StringComparison.OrdinalIgnoreCase))
        {
            return "Prepaid";
        }

        return normalized;
    }
}


