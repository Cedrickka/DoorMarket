namespace DoorMarket.Application.DTOs.Cart;

public record CartDto(
    Guid CartId,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal,
    string Currency
);
