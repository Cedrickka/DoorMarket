namespace DoorMarket.Web.Models;

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

public record ShopOrderCustomerDto(
    string DeliveryName,
    string DeliveryPhone,
    string DeliveryLine1,
    string DeliveryCity,
    string DeliveryCountry,
    string? DeliveryNotes
);

public record ShopOrderDetailDto(
    Guid Id,
    string Status,
    string PaymentStatus,
    string FulfillmentStatus,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAtUtc,
    ShopOrderCustomerDto Delivery,
    PaymentMethodSnapshotDto? PaymentMethodSnapshot,
    IReadOnlyList<OrderStatusHistoryDto> History,
    IReadOnlyList<OrderItemDto> Items
);

public record OrderStatusHistoryDto(
    string OldStatus,
    string NewStatus,
    Guid? ChangedByUserId,
    DateTime ChangedAtUtc,
    string? Note
);
