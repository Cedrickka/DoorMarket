namespace DoorMarket.Application.DTOs.Cart;

public record PromoQuoteDto(
    string? PromoCode,
    bool Applied,
    decimal Discount,
    string Message
);
