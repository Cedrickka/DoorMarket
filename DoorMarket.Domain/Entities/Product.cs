using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class Product : AuditableEntity
{
    public Guid ShopId { get; set; }
    public Shop Shop { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public string Name { get; set; } = "";

    public string? MainImageUrl { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }
    public decimal PlatformFeeAmount { get; set; } = 0m;
    public string PlatformFeeMode { get; set; } = "Flat"; // Flat | Percent
    public decimal? PlatformFeePercent { get; set; }
    public string Currency { get; set; } = "USD";

    public bool IsPromotionEnabled { get; set; }
    public decimal? PromotionPrice { get; set; }
    public DateTime? PromotionStartUtc { get; set; }
    public DateTime? PromotionEndUtc { get; set; }

    public int StockQty { get; set; }
    public bool IsActive { get; set; } = true;

    public bool HasActivePromotion(DateTime utcNow)
    {
        if (!IsPromotionEnabled || !PromotionPrice.HasValue)
        {
            return false;
        }

        if (PromotionPrice.Value <= 0m || PromotionPrice.Value >= Price)
        {
            return false;
        }

        if (PromotionStartUtc.HasValue && utcNow < PromotionStartUtc.Value)
        {
            return false;
        }

        if (PromotionEndUtc.HasValue && utcNow > PromotionEndUtc.Value)
        {
            return false;
        }

        return true;
    }

    public decimal GetEffectivePrice(DateTime utcNow)
        => HasActivePromotion(utcNow) ? PromotionPrice!.Value : Price;

    public decimal? GetPromotionPercent()
    {
        if (!PromotionPrice.HasValue || Price <= 0m)
        {
            return null;
        }

        if (PromotionPrice.Value <= 0m || PromotionPrice.Value >= Price)
        {
            return null;
        }

        var ratio = (Price - PromotionPrice.Value) / Price;
        return decimal.Round(ratio * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public decimal ResolvePlatformFee(decimal unitPrice)
    {
        if (unitPrice <= 0m)
        {
            return 0m;
        }

        if (string.Equals(PlatformFeeMode, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var percent = PlatformFeePercent ?? 0m;
            if (percent <= 0m)
            {
                return 0m;
            }

            var fee = decimal.Round(unitPrice * (percent / 100m), 2, MidpointRounding.AwayFromZero);
            return decimal.Min(unitPrice, decimal.Max(0m, fee));
        }

        return decimal.Min(unitPrice, decimal.Max(0m, PlatformFeeAmount));
    }
}
