using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class AbandonedCartEvent : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;

    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
    public decimal Subtotal { get; set; }

    public DateTime LastCartActivityAtUtc { get; set; }
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    public string ExperimentGroup { get; set; } = "A"; // A=reminder, B=holdout

    // Pending | Sent | Partial | Failed | Skipped
    public string ReminderStatus { get; set; } = "Pending";
    public int ReminderAttemptCount { get; set; }
    public string? RecipientEmail { get; set; }
    public string? SentChannels { get; set; }
    public string? Error { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
}
