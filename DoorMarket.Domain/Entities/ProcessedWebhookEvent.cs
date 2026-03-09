using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ProcessedWebhookEvent : AuditableEntity
{
    public string Provider { get; set; } = "";
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
}
