namespace DoorMarket.Application.DTOs.Cart;

public record PreCheckoutRequest(
    Guid? DeliveryZoneId,
    string? PromoCode,
    string? PaymentProvider,
    string? PrepaidCardCode,
    bool? RequireDeliveryZone
);

public record CheckoutIssueDto(
    string Code,
    string Message,
    string? ProductName
);

public record CheckoutReadinessDto(
    bool IsReady,
    string Currency,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal DeliveryFee,
    decimal TotalEstimate,
    string PaymentProvider,
    IReadOnlyList<CheckoutIssueDto> BlockingIssues,
    IReadOnlyList<CheckoutIssueDto> Warnings
);
