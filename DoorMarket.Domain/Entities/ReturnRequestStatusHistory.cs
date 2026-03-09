using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ReturnRequestStatusHistory : AuditableEntity
{
    public Guid ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = default!;

    public string OldStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
