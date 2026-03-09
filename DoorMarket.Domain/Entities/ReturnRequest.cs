using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ReturnRequest : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Status { get; set; } = "Requested"; // Requested | Approved | Rejected | Refunded | Cancelled
    public string? ReasonCode { get; set; }
    public string Reason { get; set; } = "";
    public string? Comment { get; set; }

    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Currency { get; set; } = "USD";

    public string? AdminNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public DateTime? SlaTargetAtUtc { get; set; }
    public DateTime? LastStatusChangedAtUtc { get; set; }
    public List<ReturnRequestStatusHistory> StatusHistory { get; set; } = new();
}
