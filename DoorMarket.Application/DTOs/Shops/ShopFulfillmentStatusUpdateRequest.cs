namespace DoorMarket.Application.DTOs.Shops;

public record ShopFulfillmentStatusUpdateRequest(
    string Status,
    string? Note
);
