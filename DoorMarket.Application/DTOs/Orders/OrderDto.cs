namespace DoorMarket.Application.DTOs.Orders;

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Qty,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency
);

public record OrderDto(
    Guid Id,
    string Status,
    string PaymentStatus,
    string FulfillmentStatus,
    string PaymentProvider,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAtUtc,
    PaymentMethodSnapshotDto? PaymentMethodSnapshot,
    IReadOnlyList<OrderItemDto> Items
);

public record PaymentMethodSnapshotDto(
    string Provider,
    string? CardBrand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    string? Country,
    string? Funding,
    string? ProviderPaymentIntentId,
    string? ProviderChargeId
);
