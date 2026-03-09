using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class NotificationIncidentAcknowledgement : AuditableEntity
{
    public Guid? OrderId { get; set; }
    public Guid LastLogId { get; set; }
    public string NotificationType { get; set; } = "";
    public string Recipient { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AcknowledgedBy { get; set; } = "";
    public DateTime AcknowledgedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public string? ReopenedBy { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
}
