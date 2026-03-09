using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Api.Utils;
using DoorMarket.Api.Services;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    private readonly IPromoService _promo;
    private readonly ICurrentUserService _current;
    private readonly DoorMarketDbContext _db;

    public CartController(
        ICartService cart,
        IPromoService promo,
        ICurrentUserService current,
        DoorMarketDbContext db)
    {
        _cart = cart;
        _promo = promo;
        _current = current;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct)
        => Ok((await _cart.GetMyCartAsync(ct)).ToPublicUrls(Request));

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> Add([FromBody] AddCartItemRequest req, CancellationToken ct)
        => Ok((await _cart.AddItemAsync(req.ProductId, req.Qty, ct)).ToPublicUrls(Request));

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> Update(Guid itemId, [FromBody] UpdateCartItemRequest req, CancellationToken ct)
        => Ok((await _cart.UpdateItemAsync(itemId, req.Qty, ct)).ToPublicUrls(Request));

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> Remove(Guid itemId, CancellationToken ct)
        => Ok((await _cart.RemoveItemAsync(itemId, ct)).ToPublicUrls(Request));

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _cart.ClearAsync(ct);
        return NoContent();
    }

    [HttpPost("apply-promo")]
    public async Task<ActionResult<PromoQuoteDto>> ApplyPromo([FromBody] ApplyPromoRequest req, CancellationToken ct)
    {
        var cart = await _cart.GetMyCartAsync(ct);
        var quote = _promo.Evaluate(req.Code, cart.Subtotal);
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        _db.PromoAuditLogs.Add(new PromoAuditLog
        {
            UserId = userId,
            OrderId = null,
            Source = "CartApplyPromo",
            PromoCode = NormalizeAuditPromoCode(quote.PromoCode ?? req.Code),
            Subtotal = cart.Subtotal,
            Discount = quote.Discount,
            Currency = string.IsNullOrWhiteSpace(cart.Currency) ? "USD" : cart.Currency.Trim().ToUpperInvariant(),
            Applied = quote.Applied,
            Message = quote.Message
        });
        await _db.SaveChangesAsync(ct);

        return Ok(quote);
    }

    [HttpPost("validate-coupon")]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<CouponValidationDto>> ValidateCoupon([FromBody] CouponValidationRequest req, CancellationToken ct)
    {
        var code = (req.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Code coupon requis.");
        }

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var blockingIssues = new List<CheckoutIssueDto>();
        var warnings = new List<CheckoutIssueDto>();

        var cart = await _cart.GetMyCartAsync(ct);
        if (cart.Items.Count == 0)
        {
            blockingIssues.Add(new CheckoutIssueDto("empty_cart", "Panier vide.", null));
            return Ok(new CouponValidationDto(
                IsReady: false,
                PromoCode: null,
                Applied: false,
                Message: "Panier vide.",
                Currency: "USD",
                ItemCount: 0,
                Subtotal: 0m,
                Discount: 0m,
                DeliveryFee: 0m,
                TotalEstimate: 0m,
                BlockingIssues: blockingIssues,
                Warnings: warnings));
        }

        var currency = string.IsNullOrWhiteSpace(cart.Currency) ? "USD" : cart.Currency.Trim().ToUpperInvariant();
        var subtotal = decimal.Round(cart.Subtotal, 2, MidpointRounding.AwayFromZero);
        var quote = _promo.Evaluate(code, subtotal);
        var discount = quote.Applied ? quote.Discount : 0m;

        if (!quote.Applied)
        {
            warnings.Add(new CheckoutIssueDto("coupon_not_applied", quote.Message, null));
        }

        var deliveryFee = 0m;
        if (req.DeliveryZoneId.HasValue)
        {
            var zoneFee = await _db.DeliveryZones.AsNoTracking()
                .Where(z => z.Id == req.DeliveryZoneId.Value && z.IsActive)
                .Select(z => (decimal?)z.FeeUsd)
                .FirstOrDefaultAsync(ct);
            if (!zoneFee.HasValue)
            {
                blockingIssues.Add(new CheckoutIssueDto("invalid_delivery_zone", "Zone de livraison invalide ou inactive.", null));
            }
            else
            {
                deliveryFee = decimal.Round(zoneFee.Value, 2, MidpointRounding.AwayFromZero);
            }
        }
        else if (req.RequireDeliveryZone.GetValueOrDefault(false))
        {
            blockingIssues.Add(new CheckoutIssueDto("delivery_zone_required", "Zone de livraison obligatoire.", null));
        }

        var total = decimal.Round(subtotal - discount + deliveryFee, 2, MidpointRounding.AwayFromZero);
        if (total < 0m)
        {
            total = 0m;
        }

        _db.PromoAuditLogs.Add(new PromoAuditLog
        {
            UserId = userId,
            OrderId = null,
            Source = "CartValidateCoupon",
            PromoCode = NormalizeAuditPromoCode(quote.PromoCode ?? code),
            Subtotal = subtotal,
            Discount = discount,
            Currency = currency,
            Applied = quote.Applied,
            Message = quote.Message
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new CouponValidationDto(
            IsReady: blockingIssues.Count == 0,
            PromoCode: quote.PromoCode,
            Applied: quote.Applied,
            Message: quote.Message,
            Currency: currency,
            ItemCount: cart.Items.Sum(x => x.Qty),
            Subtotal: subtotal,
            Discount: discount,
            DeliveryFee: deliveryFee,
            TotalEstimate: total,
            BlockingIssues: blockingIssues,
            Warnings: warnings));
    }

    [HttpPost("pre-checkout")]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<CheckoutReadinessDto>> PreCheckout([FromBody] PreCheckoutRequest? req, CancellationToken ct)
    {
        req ??= new PreCheckoutRequest(null, null, null, null, null);

        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        var now = DateTime.UtcNow;
        var cart = await _db.Carts.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Shop)
            .FirstOrDefaultAsync(ct);

        var blockingIssues = new List<CheckoutIssueDto>();
        var warnings = new List<CheckoutIssueDto>();
        if (cart is null || cart.Items.Count == 0)
        {
            blockingIssues.Add(new CheckoutIssueDto("empty_cart", "Panier vide.", null));
            return Ok(new CheckoutReadinessDto(
                false,
                "USD",
                0,
                0m,
                0m,
                0m,
                0m,
                NormalizePaymentProvider(req.PaymentProvider),
                blockingIssues,
                warnings));
        }

        var itemCount = 0;
        var subtotal = 0m;
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in cart.Items)
        {
            itemCount += item.Qty;
            var product = item.Product;
            if (product is null)
            {
                blockingIssues.Add(new CheckoutIssueDto("product_missing", "Produit introuvable dans le panier.", null));
                continue;
            }

            if (!product.IsActive)
            {
                blockingIssues.Add(new CheckoutIssueDto("product_inactive", $"Produit indisponible: {product.Name}", product.Name));
            }

            if (product.Shop is null || !product.Shop.IsVerified)
            {
                blockingIssues.Add(new CheckoutIssueDto("shop_unverified", $"Boutique non verifiee: {product.Shop?.Name ?? "inconnue"}", product.Name));
            }

            if (product.StockQty < item.Qty)
            {
                blockingIssues.Add(new CheckoutIssueDto("stock_insufficient", $"Stock insuffisant: {product.Name}", product.Name));
            }

            var currency = string.IsNullOrWhiteSpace(product.Currency) ? "USD" : product.Currency.Trim().ToUpperInvariant();
            currencies.Add(currency);

            var effectiveUnitPrice = product.GetEffectivePrice(now);
            subtotal += effectiveUnitPrice * item.Qty;
        }

        if (currencies.Count > 1)
        {
            blockingIssues.Add(new CheckoutIssueDto("mixed_currency", "Panier invalide: plusieurs devises.", null));
        }

        var currencyOut = currencies.Count == 1 ? currencies.First() : "USD";
        subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);

        var promoCode = (req.PromoCode ?? string.Empty).Trim();
        var promo = _promo.Evaluate(promoCode, subtotal);
        var discount = promo.Applied ? promo.Discount : 0m;
        if (!string.IsNullOrWhiteSpace(promoCode) && !promo.Applied)
        {
            warnings.Add(new CheckoutIssueDto("promo_not_applied", promo.Message, null));
        }

        var requireDeliveryZone = req.RequireDeliveryZone.GetValueOrDefault(false);
        var deliveryFee = 0m;
        if (req.DeliveryZoneId.HasValue)
        {
            var zoneFee = await _db.DeliveryZones.AsNoTracking()
                .Where(z => z.Id == req.DeliveryZoneId.Value && z.IsActive)
                .Select(z => (decimal?)z.FeeUsd)
                .FirstOrDefaultAsync(ct);

            if (!zoneFee.HasValue)
            {
                blockingIssues.Add(new CheckoutIssueDto("invalid_delivery_zone", "Zone de livraison invalide ou inactive.", null));
            }
            else
            {
                deliveryFee = decimal.Round(zoneFee.Value, 2, MidpointRounding.AwayFromZero);
            }
        }
        else if (requireDeliveryZone)
        {
            blockingIssues.Add(new CheckoutIssueDto("delivery_zone_required", "Zone de livraison obligatoire.", null));
        }
        else
        {
            warnings.Add(new CheckoutIssueDto("delivery_zone_missing", "Zone de livraison non selectionnee. Elle sera demandee au checkout.", null));
        }

        var paymentProvider = NormalizePaymentProvider(req.PaymentProvider);
        if (string.Equals(paymentProvider, "PrepaidCard", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(req.PrepaidCardCode))
        {
            blockingIssues.Add(new CheckoutIssueDto("prepaid_code_required", "Code carte prepayee requis.", null));
        }

        var total = decimal.Round(subtotal - discount + deliveryFee, 2, MidpointRounding.AwayFromZero);
        if (total < 0m)
        {
            total = 0m;
        }

        return Ok(new CheckoutReadinessDto(
            blockingIssues.Count == 0,
            currencyOut,
            itemCount,
            subtotal,
            discount,
            deliveryFee,
            total,
            paymentProvider,
            blockingIssues,
            warnings));
    }

    [HttpGet("recovery-status")]
    public async Task<ActionResult<CartRecoveryStatusDto>> GetRecoveryStatus(
        [FromQuery] int lookbackDays = 14,
        CancellationToken ct = default)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifie.");
        lookbackDays = Math.Clamp(lookbackDays, 1, 90);
        var fromUtc = DateTime.UtcNow.AddDays(-lookbackDays);

        var latestEvent = await _db.AbandonedCartEvents.AsNoTracking()
            .Where(x => x.UserId == userId && x.DetectedAtUtc >= fromUtc)
            .OrderByDescending(x => x.DetectedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (latestEvent is null)
        {
            return Ok(new CartRecoveryStatusDto(
                HasActiveReminder: false,
                Message: "Aucune relance panier recente.",
                LastDetectedAtUtc: null,
                ReminderStatus: null,
                ExperimentGroup: null,
                ItemCount: 0,
                Subtotal: 0m,
                Currency: "USD",
                HasRecentOrder: false,
                CheckoutPath: "/checkout"));
        }

        var cartSnapshot = await _db.CartItems.AsNoTracking()
            .Where(x => x.Cart.UserId == userId)
            .Select(x => new
            {
                x.Qty,
                x.UnitPrice,
                Currency = x.Product.Currency
            })
            .ToListAsync(ct);

        var hasCartItems = cartSnapshot.Count > 0;
        var itemCount = cartSnapshot.Sum(x => x.Qty);
        var subtotal = decimal.Round(cartSnapshot.Sum(x => x.UnitPrice * x.Qty), 2, MidpointRounding.AwayFromZero);
        var currency = cartSnapshot.Count == 0
            ? latestEvent.Currency
            : ResolveCurrency(cartSnapshot.Select(x => x.Currency));

        var hasRecentOrder = await _db.Orders.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.CreatedAtUtc >= latestEvent.DetectedAtUtc, ct);

        var hasActiveReminder =
            hasCartItems &&
            !hasRecentOrder &&
            !string.Equals(latestEvent.ReminderStatus, NotificationEvents.StatusSkipped, StringComparison.OrdinalIgnoreCase);

        var message = BuildRecoveryMessage(hasActiveReminder, latestEvent.ReminderStatus, hasRecentOrder, hasCartItems);

        return Ok(new CartRecoveryStatusDto(
            HasActiveReminder: hasActiveReminder,
            Message: message,
            LastDetectedAtUtc: latestEvent.DetectedAtUtc,
            ReminderStatus: latestEvent.ReminderStatus,
            ExperimentGroup: latestEvent.ExperimentGroup,
            ItemCount: itemCount,
            Subtotal: subtotal,
            Currency: currency,
            HasRecentOrder: hasRecentOrder,
            CheckoutPath: "/checkout"));
    }

    private static string? NormalizeAuditPromoCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private static string NormalizePaymentProvider(string? paymentProvider)
    {
        var normalized = (paymentProvider ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "paypal" => "PayPal",
            "prepaidcard" => "PrepaidCard",
            "prepaid" => "PrepaidCard",
            "stripe" => "Stripe",
            _ => "PayPal"
        };
    }

    private static string BuildRecoveryMessage(bool hasActiveReminder, string status, bool hasRecentOrder, bool hasCartItems)
    {
        if (hasRecentOrder)
        {
            return "Aucune relance necessaire: commande recente detectee.";
        }

        if (!hasCartItems)
        {
            return "Votre panier est vide.";
        }

        if (!hasActiveReminder)
        {
            return "Panier detecte, relance actuellement inactive.";
        }

        return status switch
        {
            "Sent" => "Vous avez des articles en attente. Reprenez votre checkout.",
            "Partial" => "Relance envoyee partiellement. Reprenez votre checkout.",
            "Failed" => "Relance echouee mais votre panier est disponible. Reprenez votre checkout.",
            _ => "Reprenez votre checkout pour finaliser la commande."
        };
    }

    private static string ResolveCurrency(IEnumerable<string?> values)
    {
        var first = values
            .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(first))
        {
            return "USD";
        }

        return first.Length <= 8 ? first : first[..8];
    }
}
