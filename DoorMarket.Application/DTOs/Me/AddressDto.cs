namespace DoorMarket.Application.DTOs.Me;

public record AddressDto(
    Guid Id,
    string Label,
    string FullName,
    string Phone,
    string Country,
    string City,
    string District,
    Guid? DeliveryZoneId,
    string? DeliveryZoneName,
    string Street,
    string? Landmark,
    bool IsDefault
);

public record CreateAddressRequest(
    string Label,
    string FullName,
    string Phone,
    string Country,
    string City,
    string District,
    Guid? DeliveryZoneId,
    string Street,
    string? Landmark,
    bool IsDefault
);

public record UpdateAddressRequest(
    string Label,
    string FullName,
    string Phone,
    string Country,
    string City,
    string District,
    Guid? DeliveryZoneId,
    string Street,
    string? Landmark,
    bool IsDefault
);
