namespace DoorMarket.Application.DTOs.Cart;

public record AddCartItemRequest(Guid ProductId, int Qty);
