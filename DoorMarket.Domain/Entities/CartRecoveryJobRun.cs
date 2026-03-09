using DoorMarket.Domain.Common;

namespace DoorMarket.Domain.Entities;

public class CartRecoveryJobRun : AuditableEntity
{
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndedAtUtc { get; set; } = DateTime.UtcNow;
    public int DurationMs { get; set; }
    public bool Success { get; set; }

    public int CandidatesScanned { get; set; }
    public int EventsCreated { get; set; }
    public int RemindersSent { get; set; }
    public int RemindersFailed { get; set; }
    public int AntiSpamSkipped { get; set; }
    public int ConvertedSkipped { get; set; }

    public string? Error { get; set; }
}

