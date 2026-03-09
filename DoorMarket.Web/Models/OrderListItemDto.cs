namespace DoorMarket.Web.Models;

public record OrderListItemDto(
    Guid Id,
    string Status,
    string PaymentStatus,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAtUtc
);
