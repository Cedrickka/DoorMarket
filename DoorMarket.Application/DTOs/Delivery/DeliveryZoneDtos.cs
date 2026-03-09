namespace DoorMarket.Application.DTOs.Delivery;

public record DeliveryZoneDto(
    Guid Id,
    string Code,
    string Name,
    string Country,
    string? StateCode,
    decimal FeeUsd,
    bool IsActive
);

public record UpsertDeliveryZoneRequest(
    string Code,
    string Name,
    string Country,
    string? StateCode,
    decimal FeeUsd,
    bool IsActive
);
