namespace DoorMarket.Application.DTOs.Cart;

public record CartRecoveryStatusDto(
    bool HasActiveReminder,
    string Message,
    DateTime? LastDetectedAtUtc,
    string? ReminderStatus,
    string? ExperimentGroup,
    int ItemCount,
    decimal Subtotal,
    string Currency,
    bool HasRecentOrder,
    string CheckoutPath
);

