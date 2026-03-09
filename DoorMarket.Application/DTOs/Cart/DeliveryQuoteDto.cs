namespace DoorMarket.Application.DTOs.Cart;

public record DeliveryQuoteDto(
    decimal Subtotal,
    decimal DeliveryFee,
    string Currency
);
