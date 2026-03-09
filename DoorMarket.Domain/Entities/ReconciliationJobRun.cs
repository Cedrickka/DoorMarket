using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class ReconciliationJobRun : AuditableEntity
{
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationMs { get; set; }
    public bool Success { get; set; }
    public bool InProgress { get; set; }
    public bool WasSkipped { get; set; }
    public string TriggerSource { get; set; } = "Worker";
    public Guid? TriggeredByUserId { get; set; }
    public DateTime WindowFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime WindowToUtc { get; set; } = DateTime.UtcNow;
    public int DraftSlaDays { get; set; } = 7;
    public int ApprovedSlaDays { get; set; } = 3;
    public int CandidatePayouts { get; set; }
    public int StaleDraftCount { get; set; }
    public int StaleApprovedCount { get; set; }
    public int PartialPaidCount { get; set; }
    public int PaidWithoutReferenceCount { get; set; }
    public int OverlapPairCount { get; set; }
    public int ReversedCount { get; set; }
    public int InsightsCount { get; set; }
    public decimal ReversalRatePercent { get; set; }
    public decimal PartialGapTotal { get; set; }
    public string? SkipReason { get; set; }
    public string? Error { get; set; }
}
