using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class TransactionalNotificationLog : AuditableEntity
{
    public Guid? OrderId { get; set; }
    public string NotificationType { get; set; } = "";
    public string Channel { get; set; } = "Email";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Status { get; set; } = ""; // Sent | Failed
    public string? Error { get; set; }
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
}
