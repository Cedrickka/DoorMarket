namespace DoorMarket.Application.DTOs.Cart;

public record CouponValidationRequest(
    string Code,
    Guid? DeliveryZoneId,
    bool? RequireDeliveryZone
);

public record CouponValidationDto(
    bool IsReady,
    string? PromoCode,
    bool Applied,
    string Message,
    string Currency,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal DeliveryFee,
    decimal TotalEstimate,
    IReadOnlyList<CheckoutIssueDto> BlockingIssues,
    IReadOnlyList<CheckoutIssueDto> Warnings
);
