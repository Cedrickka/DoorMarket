namespace DoorMarket.Application.DTOs.Orders;

public record CheckoutRequest(
    string DeliveryName,
    string DeliveryPhone,
    string DeliveryLine1,
    string DeliveryCity,
    string DeliveryCountry,
    Guid? DeliveryZoneId,
    string? DeliveryNotes,
    string? PromoCode,
    string? PaymentProvider,
    string? PrepaidCardCode
);
