namespace DoorMarket.Application.DTOs.Orders;

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
