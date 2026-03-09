using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class CheckoutAnalyticsEvent : AuditableEntity
{
    public Guid? UserId { get; set; }
    public Guid? OrderId { get; set; }
    public string? SessionId { get; set; }
    public string EventType { get; set; } = "CheckoutViewed";
    public string? PaymentProvider { get; set; }
    public string? PaymentChannel { get; set; }
    public string? ExperimentName { get; set; }
    public string? ExperimentGroup { get; set; }
    public bool? Success { get; set; }
    public int? DurationMs { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Source { get; set; }
    public string? CountryTag { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
