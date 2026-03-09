using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Cart;
using DoorMarket.Application.Interfaces.Orders;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DoorMarket.Application.DTOs.Shops;


namespace DoorMarket.Infrastructure.Orders;

public class OrderService : IOrderService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPromoService _promo;

    public OrderService(
        DoorMarketDbContext db,
        ICurrentUserService current,
        IPromoService promo)
    {
        _db = db;
        _current = current;
        _promo = promo;
    }

    public async Task<OrderDto> CheckoutAsync(CheckoutRequest req, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId, ct)
                   ?? throw new InvalidOperationException("Panier vide.");

        var cartItems = await _db.CartItems
            .Where(i => i.CartId == cart.Id)
            .Include(i => i.Product)
            .ThenInclude(p => p.Shop)
            .ToListAsync(ct);

        if (cartItems.Count == 0) throw new InvalidOperationException("Panier vide.");

        // validations V1
        foreach (var i in cartItems)
        {
            if (i.Product is null)
            {
                throw new InvalidOperationException("Panier invalide: produit introuvable.");
            }

            if (i.Product.Shop is null)
            {
                throw new InvalidOperationException($"Panier invalide: boutique introuvable pour {i.Product.Name}.");
            }

            if (!i.Product.IsActive) throw new InvalidOperationException($"Produit indisponible: {i.Product.Name}");
            if (!i.Product.Shop.IsVerified) throw new InvalidOperationException($"Boutique non vérifiée: {i.Product.Shop.Name}");
            if (i.Product.StockQty < i.Qty) throw new InvalidOperationException($"Stock insuffisant: {i.Product.Name}");
        }

        // ✅ V1: on impose une seule devise
        var currency = cartItems.Select(x => x.Product.Currency).Distinct().ToList();
        if (currency.Count != 1) throw new InvalidOperationException("Panier invalide: plusieurs devises.");

        var cur = currency[0];
        var subtotal = cartItems.Sum(i => i.UnitPrice * i.Qty);
        var dynamicCommissionRules = await LoadActiveCommissionRulesAsync(cartItems, ct);
        var platformFeeTotal = cartItems.Sum(i => ResolvePlatformFee(i.Product, i.UnitPrice, dynamicCommissionRules) * i.Qty);
        var deliveryFee = await ComputeDeliveryFeeAsync(req.DeliveryZoneId, ct);
        var promoInput = string.IsNullOrWhiteSpace(req.PromoCode) ? null : req.PromoCode.Trim();
        var promoQuote = _promo.Evaluate(promoInput, subtotal);
        var discount = promoQuote.Discount;
        var appliedPromoCode = promoQuote.Applied ? promoQuote.PromoCode : null;
        var totalAmount = decimal.Round(subtotal + deliveryFee - discount, 2, MidpointRounding.AwayFromZero);

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = new Domain.Entities.Order
        {
            UserId = userId,
            Status = "Created",
            PaymentStatus = "Unpaid",
            FulfillmentStatus = "PendingPayment",
            PaymentProvider = NormalizePaymentProvider(req.PaymentProvider),
            Currency = cur,
            Subtotal = subtotal,
            TotalItemsAmount = subtotal,
            PlatformFeeTotal = platformFeeTotal,
            DeliveryFee = deliveryFee,
            Discount = discount,
            PromoCode = appliedPromoCode,
            TotalAmount = totalAmount,

            DeliveryName = req.DeliveryName.Trim(),
            DeliveryPhone = req.DeliveryPhone.Trim(),
            DeliveryLine1 = req.DeliveryLine1.Trim(),
            DeliveryCity = req.DeliveryCity.Trim(),
            DeliveryCountry = req.DeliveryCountry.Trim(),
            DeliveryNotes = string.IsNullOrWhiteSpace(req.DeliveryNotes) ? null : req.DeliveryNotes.Trim()
        };

        _db.Orders.Add(order);

        foreach (var i in cartItems)
        {
            var unitPrice = i.UnitPrice;
            var platformFee = ResolvePlatformFee(i.Product, unitPrice, dynamicCommissionRules);
            _db.OrderItems.Add(new Domain.Entities.OrderItem
            {
                Order = order,
                ProductId = i.ProductId,
                Qty = i.Qty,
                UnitPrice = unitPrice,
                UnitPriceAtPurchase = unitPrice,
                PlatformFeeAtPurchase = platformFee,
                LineTotal = unitPrice * i.Qty
            });
        }

        if (!string.IsNullOrWhiteSpace(promoInput))
        {
            _db.PromoAuditLogs.Add(new Domain.Entities.PromoAuditLog
            {
                UserId = userId,
                OrderId = order.Id,
                Source = "Checkout",
                PromoCode = NormalizeAuditPromoCode(promoQuote.PromoCode ?? promoInput),
                Subtotal = subtotal,
                Discount = discount,
                Currency = cur,
                Applied = promoQuote.Applied,
                Message = promoQuote.Message
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetOrderDtoAsync(order.Id, userId, ct)
               ?? throw new InvalidOperationException("Erreur création commande.");
    }

    public async Task<PagedResult<OrderDto>> GetMyOrdersAsync(int page, int pageSize, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.Orders.AsNoTracking().Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAtUtc);

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id,
                o.Status,
                o.PaymentStatus,
                o.FulfillmentStatus,
                o.PaymentProvider,
                o.Subtotal,
                o.DeliveryFee,
                o.Discount,
                o.TotalAmount,
                o.Currency,
                o.CreatedAtUtc,
                null,
                new List<OrderItemDto>()))
            .ToListAsync(ct);

        return new PagedResult<OrderDto> { Page = page, PageSize = pageSize, Total = total, Items = items };
    }

    public async Task<OrderDto?> GetMyOrderByIdAsync(Guid id, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        return await GetOrderDtoAsync(id, userId, ct);
    }

    // SHOP
    public async Task<PagedResult<OrderDto>> GetMyShopOrdersAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
            ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var baseQuery = _db.Orders.AsNoTracking()
            .Where(o =>
                _db.OrderItems.Any(oi => oi.OrderId == o.Id && oi.Product.ShopId == shop.Id));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            baseQuery = baseQuery.Where(o => o.FulfillmentStatus == s || o.Status == s);
        }

        var total = await baseQuery.CountAsync(ct);

        var orderIds = await baseQuery
            .OrderByDescending(o => o.CreatedAtUtc)  // ✅ tri correct
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => o.Id)
            .ToListAsync(ct);

        var list = new List<OrderDto>();
        foreach (var id in orderIds)
        {
            var dto = await GetOrderDtoAsyncForShop(id, shop.Id, ct);
            if (dto != null) list.Add(dto);
        }

        return new PagedResult<OrderDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = list
        };
    }
    public async Task SetStatusForMyShopAsync(Guid orderId, string status, CancellationToken ct)
        => await SetFulfillmentStatusForMyShopAsync(orderId, status switch
        {
            "Preparing" => "Processing",
            "Ready" => "Delivered",
            _ => status
        }, note: null, ct);

    public async Task SetFulfillmentStatusForMyShopAsync(Guid orderId, string status, string? note, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");
        var isAdmin = string.Equals(_current.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(_current.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                       ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

            // Vérifie que l’ordre contient au moins un produit de cette boutique
            var has = await _db.OrderItems
                .Include(oi => oi.Product)
                .AnyAsync(oi => oi.OrderId == orderId && oi.Product.ShopId == shop.Id, ct);

            if (!has) throw new InvalidOperationException("Accès refusé.");
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new InvalidOperationException("Commande introuvable.");

        var target = NormalizeFulfillmentStatus(status);
        ValidateFulfillmentTransition(order.FulfillmentStatus, target, _current.Role);

        if (!string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) &&
            target is "PaidPending" or "Processing" or "Delivered")
        {
            throw new InvalidOperationException("Impossible : commande non payée.");
        }

        var previous = order.FulfillmentStatus;
        order.FulfillmentStatus = target;
        order.Status = MapLegacyStatus(target);
        order.UpdatedAtUtc = DateTime.UtcNow;
        if (string.Equals(target, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            order.DeliveredAtUtc = DateTime.UtcNow;
        }

        _db.OrderStatusHistories.Add(new Domain.Entities.OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = previous,
            NewStatus = target,
            ChangedByUserId = userId,
            ChangedAtUtc = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<OrderDto?> GetOrderDtoAsync(Guid orderId, Guid userId, CancellationToken ct)
    {
        var o = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, ct);
        if (o is null) return null;

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Include(i => i.Product)
            .Select(i => new OrderItemDto(i.ProductId, i.Product.Name, i.Qty, i.UnitPrice, i.LineTotal, i.Product.Currency))
            .ToListAsync(ct);

        var snapshot = await _db.PaymentMethodSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);

        return new OrderDto(
                    o.Id,
                    o.Status,
                    o.PaymentStatus,
                    o.FulfillmentStatus,
                    o.PaymentProvider,
                    o.Subtotal,
                    o.DeliveryFee,
                    o.Discount,
                    o.TotalAmount,
                    o.Currency,
                    o.CreatedAtUtc,
                    snapshot is null ? null : new PaymentMethodSnapshotDto(
                        snapshot.Provider,
                        snapshot.CardBrand,
                        snapshot.Last4,
                        snapshot.ExpMonth,
                        snapshot.ExpYear,
                        snapshot.Country,
                        snapshot.Funding,
                        snapshot.ProviderPaymentIntentId,
                        snapshot.ProviderChargeId),
                    items
                );

    }

    private async Task<OrderDto?> GetOrderDtoAsyncForShop(Guid orderId, Guid shopId, CancellationToken ct)
    {
        var o = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return null;

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId && i.Product.ShopId == shopId)
            .Include(i => i.Product)
            .Select(i => new OrderItemDto(i.ProductId, i.Product.Name, i.Qty, i.UnitPrice, i.LineTotal, i.Product.Currency))
            .ToListAsync(ct);

        var snapshot = await _db.PaymentMethodSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);

        return new OrderDto(
                    o.Id,
                    o.Status,
                    o.PaymentStatus,
                    o.FulfillmentStatus,
                    o.PaymentProvider,
                    o.Subtotal,
                    o.DeliveryFee,
                    o.Discount,
                    o.TotalAmount,
                    o.Currency,
                    o.CreatedAtUtc,
                    snapshot is null ? null : new PaymentMethodSnapshotDto(
                        snapshot.Provider,
                        snapshot.CardBrand,
                        snapshot.Last4,
                        snapshot.ExpMonth,
                        snapshot.ExpYear,
                        snapshot.Country,
                        snapshot.Funding,
                        snapshot.ProviderPaymentIntentId,
                        snapshot.ProviderChargeId),
                    items
                );

    }

    public async Task<ShopOrderDetailDto?> GetMyShopOrderByIdAsync(Guid orderId, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var has = await _db.OrderItems
            .Include(oi => oi.Product)
            .AnyAsync(oi => oi.OrderId == orderId && oi.Product.ShopId == shop.Id, ct);

        if (!has) return null;

        var o = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return null;

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId && i.Product.ShopId == shop.Id)
            .Include(i => i.Product)
            .Select(i => new OrderItemDto(
                i.ProductId,
                i.Product.Name,
                i.Qty,
                i.UnitPrice,
                i.LineTotal,
                i.Product.Currency
            ))
            .ToListAsync(ct);

        var snapshot = await _db.PaymentMethodSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);

        var history = await _db.OrderStatusHistories.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new OrderStatusHistoryDto(
                x.OldStatus,
                x.NewStatus,
                x.ChangedByUserId,
                x.ChangedAtUtc,
                x.Note))
            .ToListAsync(ct);

        return new ShopOrderDetailDto(
            o.Id,
            o.Status,
            o.PaymentStatus,
            o.FulfillmentStatus,
            o.Subtotal,
            o.DeliveryFee,
            o.Discount,
            o.TotalAmount,
            o.Currency,
            o.CreatedAtUtc,
            new ShopOrderCustomerDto(
                o.DeliveryName,
                o.DeliveryPhone,
                o.DeliveryLine1,
                o.DeliveryCity,
                o.DeliveryCountry,
                o.DeliveryNotes
            ),
            snapshot is null ? null : new PaymentMethodSnapshotDto(
                snapshot.Provider,
                snapshot.CardBrand,
                snapshot.Last4,
                snapshot.ExpMonth,
                snapshot.ExpYear,
                snapshot.Country,
                snapshot.Funding,
                snapshot.ProviderPaymentIntentId,
                snapshot.ProviderChargeId),
            history,
            items
        );
    }

    public async Task<ShopDashboardSummaryDto> GetMyShopSummaryAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new InvalidOperationException("Non authentifié.");

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct)
                   ?? throw new InvalidOperationException("Vous n'avez pas de boutique.");

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Orders de la boutique aujourd’hui (commande qui contient au moins un produit de la boutique)
        var orderIdsToday = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Product.ShopId == shop.Id &&
                         oi.Order.CreatedAtUtc >= today && oi.Order.CreatedAtUtc < tomorrow)
            .Select(oi => oi.OrderId)
            .Distinct()
            .ToListAsync(ct);

        var ordersToday = orderIdsToday.Count;

        // Revenus du jour (uniquement payés)
        var revenueToday = await _db.OrderItems.AsNoTracking()
            .Where(oi => oi.Product.ShopId == shop.Id &&
                         oi.Order.PaymentStatus == "Paid" &&
                         oi.Order.CreatedAtUtc >= today && oi.Order.CreatedAtUtc < tomorrow)
            .SumAsync(oi => (decimal?)oi.LineTotal, ct) ?? 0m;

        var productsActive = await _db.Products.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && p.IsActive)
            .CountAsync(ct);

        var productsOutOfStock = await _db.Products.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && p.IsActive && p.StockQty <= 0)
            .CountAsync(ct);

        return new ShopDashboardSummaryDto(
            ordersToday,
            revenueToday,
            productsActive,
            productsOutOfStock
        );
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

    private static string NormalizeFulfillmentStatus(string? status)
    {
        var normalized = (status ?? string.Empty).Trim();
        return normalized switch
        {
            "PaidPending" => "PaidPending",
            "Processing" => "Processing",
            "Delivered" => "Delivered",
            "Cancelled" => "Cancelled",
            _ => throw new InvalidOperationException("Statut fulfillment invalide.")
        };
    }

    private static void ValidateFulfillmentTransition(string from, string to, string? role)
    {
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        if (isAdmin)
        {
            return;
        }

        if (string.Equals(from, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Delivered est irreversible sauf Admin.");
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allowed = from switch
        {
            "PaidPending" => to is "Processing" or "Cancelled",
            "Processing" => to is "Delivered" or "Cancelled",
            "PendingPayment" => false,
            "Cancelled" => false,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Transition non autorisee: {from} -> {to}.");
        }
    }

    private static string MapLegacyStatus(string fulfillmentStatus)
        => fulfillmentStatus switch
        {
            "PendingPayment" => "Created",
            "PaidPending" => "Paid",
            "Processing" => "Preparing",
            "Delivered" => "Delivered",
            "Cancelled" => "Cancelled",
            _ => "Created"
        };

    private async Task<decimal> ComputeDeliveryFeeAsync(Guid? deliveryZoneId, CancellationToken ct)
    {
        if (!deliveryZoneId.HasValue)
        {
            throw new InvalidOperationException("Zone de livraison obligatoire. Completez la zone de votre adresse.");
        }

        var zoneFee = await _db.DeliveryZones.AsNoTracking()
            .Where(z => z.Id == deliveryZoneId.Value && z.IsActive)
            .Select(z => (decimal?)z.FeeUsd)
            .FirstOrDefaultAsync(ct);

        if (!zoneFee.HasValue)
        {
            throw new InvalidOperationException("Zone de livraison invalide ou inactive.");
        }

        return decimal.Round(zoneFee.Value, 2);
    }

    private async Task<List<CommissionRuleCandidate>> LoadActiveCommissionRulesAsync(
        IReadOnlyList<Domain.Entities.CartItem> cartItems,
        CancellationToken ct)
    {
        if (cartItems.Count == 0)
        {
            return new List<CommissionRuleCandidate>();
        }

        var now = DateTime.UtcNow;
        var productIds = cartItems.Select(x => x.ProductId).Distinct().ToList();
        var shopIds = cartItems
            .Where(x => x.Product is not null)
            .Select(x => x.Product.ShopId)
            .Distinct()
            .ToList();
        var categoryIds = cartItems
            .Where(x => x.Product is not null)
            .Select(x => x.Product.CategoryId)
            .Distinct()
            .ToList();
        var currencies = cartItems
            .Where(x => x.Product is not null)
            .Select(x => x.Product.Currency)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        return await _db.CommissionRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => !r.StartsAtUtc.HasValue || r.StartsAtUtc.Value <= now)
            .Where(r => !r.EndsAtUtc.HasValue || r.EndsAtUtc.Value > now)
            .Where(r =>
                r.ScopeType == CommissionScopeGlobal ||
                (r.ScopeType == CommissionScopeShop && r.ScopeShopId.HasValue && shopIds.Contains(r.ScopeShopId.Value)) ||
                (r.ScopeType == CommissionScopeCategory && r.ScopeCategoryId.HasValue && categoryIds.Contains(r.ScopeCategoryId.Value)) ||
                (r.ScopeType == CommissionScopeProduct && r.ScopeProductId.HasValue && productIds.Contains(r.ScopeProductId.Value)))
            .Where(r => r.Currency == null || r.Currency == "" || currencies.Contains(r.Currency))
            .Select(r => new CommissionRuleCandidate(
                r.Id,
                r.ScopeType,
                r.ScopeShopId,
                r.ScopeCategoryId,
                r.ScopeProductId,
                r.Currency,
                r.PlatformFeeMode,
                r.PlatformFeeAmount,
                r.PlatformFeePercent,
                r.MinUnitPrice,
                r.MaxUnitPrice,
                r.Priority,
                r.CreatedAtUtc))
            .ToListAsync(ct);
    }

    private static decimal ResolvePlatformFee(
        Domain.Entities.Product? product,
        decimal unitPrice,
        IReadOnlyList<CommissionRuleCandidate> dynamicRules)
    {
        if (product is null)
        {
            return 0m;
        }

        var rule = SelectBestCommissionRule(product, unitPrice, dynamicRules);
        if (rule is not null)
        {
            var dynamicFee = ResolveFeeFromRule(rule, unitPrice);
            if (dynamicFee.HasValue)
            {
                return dynamicFee.Value;
            }
        }

        return product.ResolvePlatformFee(unitPrice);
    }

    private static CommissionRuleCandidate? SelectBestCommissionRule(
        Domain.Entities.Product product,
        decimal unitPrice,
        IReadOnlyList<CommissionRuleCandidate> rules)
    {
        if (rules.Count == 0)
        {
            return null;
        }

        var currency = (product.Currency ?? string.Empty).Trim().ToUpperInvariant();

        return rules
            .Where(r => IsRuleMatching(r, product, unitPrice, currency))
            .OrderByDescending(r => GetScopeSpecificityScore(r.ScopeType))
            .ThenBy(r => r.Priority)
            .ThenByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsRuleMatching(
        CommissionRuleCandidate rule,
        Domain.Entities.Product product,
        decimal unitPrice,
        string normalizedCurrency)
    {
        if (!string.IsNullOrWhiteSpace(rule.Currency))
        {
            var ruleCurrency = rule.Currency.Trim().ToUpperInvariant();
            if (!string.Equals(ruleCurrency, normalizedCurrency, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (rule.MinUnitPrice.HasValue && unitPrice < rule.MinUnitPrice.Value)
        {
            return false;
        }

        if (rule.MaxUnitPrice.HasValue && unitPrice > rule.MaxUnitPrice.Value)
        {
            return false;
        }

        return rule.ScopeType switch
        {
            CommissionScopeGlobal => true,
            CommissionScopeShop => rule.ScopeShopId.HasValue && rule.ScopeShopId.Value == product.ShopId,
            CommissionScopeCategory => rule.ScopeCategoryId.HasValue && rule.ScopeCategoryId.Value == product.CategoryId,
            CommissionScopeProduct => rule.ScopeProductId.HasValue && rule.ScopeProductId.Value == product.Id,
            _ => false
        };
    }

    private static int GetScopeSpecificityScore(string? scopeType)
        => scopeType switch
        {
            CommissionScopeProduct => 4,
            CommissionScopeCategory => 3,
            CommissionScopeShop => 2,
            CommissionScopeGlobal => 1,
            _ => 0
        };

    private static decimal? ResolveFeeFromRule(CommissionRuleCandidate rule, decimal unitPrice)
    {
        if (unitPrice <= 0m)
        {
            return 0m;
        }

        if (string.Equals(rule.PlatformFeeMode, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var percent = rule.PlatformFeePercent ?? 0m;
            if (percent <= 0m)
            {
                return 0m;
            }

            var computed = decimal.Round(unitPrice * (percent / 100m), 2, MidpointRounding.AwayFromZero);
            return decimal.Min(unitPrice, decimal.Max(0m, computed));
        }

        if (!string.Equals(rule.PlatformFeeMode, "Flat", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return decimal.Min(unitPrice, decimal.Max(0m, rule.PlatformFeeAmount));
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

    private const string CommissionScopeGlobal = "Global";
    private const string CommissionScopeShop = "Shop";
    private const string CommissionScopeCategory = "Category";
    private const string CommissionScopeProduct = "Product";

    private sealed record CommissionRuleCandidate(
        Guid Id,
        string ScopeType,
        Guid? ScopeShopId,
        Guid? ScopeCategoryId,
        Guid? ScopeProductId,
        string? Currency,
        string PlatformFeeMode,
        decimal PlatformFeeAmount,
        decimal? PlatformFeePercent,
        decimal? MinUnitPrice,
        decimal? MaxUnitPrice,
        int Priority,
        DateTime CreatedAtUtc);

}
