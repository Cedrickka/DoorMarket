using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.Carts;

public sealed class PromoService : IPromoService
{
    private const string DiscountTypePercent = "Percent";
    private const string DiscountTypeFixed = "Fixed";
    private const string ScopeGlobal = "Global";
    private const string ScopeShop = "Shop";
    private const string ScopeCategory = "Category";
    private const string ScopeCity = "City";
    private const string ScopeCountry = "Country";

    private readonly DoorMarketDbContext? _db;
    private readonly ICurrentUserService? _current;

    // For tests and backward compatibility.
    public PromoService()
    {
    }

    public PromoService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public PromoQuoteDto Evaluate(string? code, decimal subtotal)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return new PromoQuoteDto(null, false, 0m, "Code promo vide.");
        }

        if (subtotal <= 0m)
        {
            return new PromoQuoteDto(null, false, 0m, "Montant du panier invalide.");
        }

        var couponQuote = EvaluateCoupon(normalizedCode, subtotal);
        if (couponQuote is not null)
        {
            return couponQuote;
        }

        var legacyDiscount = CalculateLegacyDiscount(normalizedCode, subtotal);
        if (legacyDiscount <= 0m)
        {
            return new PromoQuoteDto(null, false, 0m, "Code promo invalide.");
        }

        return new PromoQuoteDto(normalizedCode, true, legacyDiscount, "Code promo applique.");
    }

    private PromoQuoteDto? EvaluateCoupon(string normalizedCode, decimal subtotal)
    {
        if (_db is null)
        {
            return null;
        }

        var coupon = _db.Coupons.AsNoTracking()
            .FirstOrDefault(x => x.Code == normalizedCode);
        if (coupon is null)
        {
            return null;
        }

        if (!coupon.IsActive)
        {
            return new PromoQuoteDto(null, false, 0m, "Coupon inactif.");
        }

        var now = DateTime.UtcNow;
        if (coupon.StartsAtUtc.HasValue && now < coupon.StartsAtUtc.Value)
        {
            return new PromoQuoteDto(null, false, 0m, "Coupon pas encore actif.");
        }

        if (coupon.EndsAtUtc.HasValue && now > coupon.EndsAtUtc.Value)
        {
            return new PromoQuoteDto(null, false, 0m, "Coupon expire.");
        }

        var discountBase = ResolveDiscountBase(coupon, subtotal);
        if (discountBase <= 0m)
        {
            return new PromoQuoteDto(null, false, 0m, "Coupon non applicable au panier.");
        }

        if (coupon.MinSubtotal.HasValue && discountBase < coupon.MinSubtotal.Value)
        {
            return new PromoQuoteDto(null, false, 0m, "Montant minimum non atteint pour ce coupon.");
        }

        var appliedDiscounts = _db.PromoAuditLogs.AsNoTracking()
            .Where(x => x.Applied && x.Source == "Checkout" && x.PromoCode == normalizedCode)
            .Select(x => x.Discount)
            .ToList();

        var usedCount = appliedDiscounts.Count;
        var usedDiscount = appliedDiscounts.Sum();

        if (coupon.UsageLimitTotal.HasValue && usedCount >= coupon.UsageLimitTotal.Value)
        {
            return new PromoQuoteDto(null, false, 0m, "Limite d'utilisation atteinte.");
        }

        if (coupon.BudgetAmount.HasValue && usedDiscount >= coupon.BudgetAmount.Value)
        {
            return new PromoQuoteDto(null, false, 0m, "Budget coupon epuise.");
        }

        var userId = _current?.UserId;
        if (coupon.UsageLimitPerUser.HasValue && userId.HasValue)
        {
            var userUses = _db.PromoAuditLogs.AsNoTracking()
                .Count(x =>
                    x.Applied &&
                    x.Source == "Checkout" &&
                    x.UserId == userId.Value &&
                    x.PromoCode == normalizedCode);
            if (userUses >= coupon.UsageLimitPerUser.Value)
            {
                return new PromoQuoteDto(null, false, 0m, "Limite d'utilisation par utilisateur atteinte.");
            }
        }

        var discount = ComputeDiscountAmount(coupon, discountBase);
        if (discount <= 0m)
        {
            return new PromoQuoteDto(null, false, 0m, "Configuration coupon invalide.");
        }

        if (coupon.BudgetAmount.HasValue)
        {
            var remainingBudget = decimal.Round(coupon.BudgetAmount.Value - usedDiscount, 2, MidpointRounding.AwayFromZero);
            if (remainingBudget <= 0m)
            {
                return new PromoQuoteDto(null, false, 0m, "Budget coupon epuise.");
            }

            discount = decimal.Min(discount, remainingBudget);
        }

        discount = decimal.Min(discount, subtotal);
        discount = decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
        if (discount <= 0m)
        {
            return new PromoQuoteDto(null, false, 0m, "Coupon non applicable.");
        }

        return new PromoQuoteDto(normalizedCode, true, discount, "Coupon applique.");
    }

    private decimal ResolveDiscountBase(Coupon coupon, decimal subtotal)
    {
        var scope = NormalizeScope(coupon.ScopeType);
        if (scope == ScopeGlobal)
        {
            return subtotal;
        }

        if (_db is null || !(_current?.UserId.HasValue ?? false))
        {
            return 0m;
        }

        var userId = _current!.UserId!.Value;
        var cart = _db.Carts.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Shop)
            .FirstOrDefault();
        if (cart is null || cart.Items.Count == 0)
        {
            return 0m;
        }

        IEnumerable<CartItem> eligible = scope switch
        {
            ScopeShop => coupon.ScopeShopId.HasValue
                ? cart.Items.Where(i => i.Product.ShopId == coupon.ScopeShopId.Value)
                : Array.Empty<CartItem>(),
            ScopeCategory => coupon.ScopeCategoryId.HasValue
                ? cart.Items.Where(i => i.Product.CategoryId == coupon.ScopeCategoryId.Value)
                : Array.Empty<CartItem>(),
            ScopeCity => !string.IsNullOrWhiteSpace(coupon.ScopeCity)
                ? cart.Items.Where(i => string.Equals(
                    (i.Product.Shop?.City ?? string.Empty).Trim(),
                    coupon.ScopeCity.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                : Array.Empty<CartItem>(),
            ScopeCountry => !string.IsNullOrWhiteSpace(coupon.ScopeCountry)
                ? cart.Items.Where(i => string.Equals(
                    (i.Product.Shop?.CountryTag ?? string.Empty).Trim(),
                    coupon.ScopeCountry.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                : Array.Empty<CartItem>(),
            _ => Array.Empty<CartItem>()
        };

        var baseAmount = eligible.Sum(i => i.UnitPrice * i.Qty);
        return decimal.Round(baseAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputeDiscountAmount(Coupon coupon, decimal discountBase)
    {
        var type = NormalizeDiscountType(coupon.DiscountType);
        if (type == DiscountTypePercent)
        {
            if (coupon.DiscountValue <= 0m || coupon.DiscountValue > 100m)
            {
                return 0m;
            }

            var percentDiscount = discountBase * (coupon.DiscountValue / 100m);
            var rounded = decimal.Round(percentDiscount, 2, MidpointRounding.AwayFromZero);
            if (coupon.MaxDiscountAmount.HasValue && coupon.MaxDiscountAmount.Value > 0m)
            {
                return decimal.Min(rounded, coupon.MaxDiscountAmount.Value);
            }

            return rounded;
        }

        if (type == DiscountTypeFixed)
        {
            if (coupon.DiscountValue <= 0m)
            {
                return 0m;
            }

            return decimal.Round(coupon.DiscountValue, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ScopeGlobal;
        }

        return scope.Trim().ToLowerInvariant() switch
        {
            "global" => ScopeGlobal,
            "shop" => ScopeShop,
            "category" => ScopeCategory,
            "city" => ScopeCity,
            "country" => ScopeCountry,
            _ => ScopeGlobal
        };
    }

    private static string NormalizeDiscountType(string? discountType)
    {
        if (string.IsNullOrWhiteSpace(discountType))
        {
            return DiscountTypePercent;
        }

        return discountType.Trim().ToLowerInvariant() switch
        {
            "percent" => DiscountTypePercent,
            "fixed" => DiscountTypeFixed,
            _ => string.Empty
        };
    }

    private static decimal CalculateLegacyDiscount(string code, decimal subtotal)
    {
        if (subtotal <= 0m)
        {
            return 0m;
        }

        var discount = code switch
        {
            "DOOR10" => decimal.Round(subtotal * 0.10m, 2, MidpointRounding.AwayFromZero),
            "WELCOME5" => decimal.Min(5m, subtotal),
            _ => 0m
        };

        if (discount < 0m)
        {
            return 0m;
        }

        return decimal.Min(discount, subtotal);
    }
}
