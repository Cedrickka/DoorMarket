using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class CheckoutAlertIncident : AuditableEntity
{
    public string AlertCode { get; set; } = "";
    public string Severity { get; set; } = "Warning";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Provider { get; set; }
    public decimal ObservedValue { get; set; }
    public decimal ThresholdValue { get; set; }
    public DateTime FirstTriggeredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastTriggeredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime WindowFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime WindowToUtc { get; set; } = DateTime.UtcNow;
    public int TriggerCount { get; set; } = 1;
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? AcknowledgementNote { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? LastNotifiedAtUtc { get; set; }
    public int NotificationCount { get; set; } = 0;
    public string? LastNotificationError { get; set; }
}
