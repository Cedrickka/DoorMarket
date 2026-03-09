namespace DoorMarket.Application.DTOs.Cart;

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    Guid? ShopId,
    string ProductName,
    decimal UnitPrice,
    int Qty,
    decimal LineTotal,
    string Currency,
    string? MainImageUrl
);
