using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class ReconciliationJobRunConfiguration : IEntityTypeConfiguration<ReconciliationJobRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationJobRun> b)
    {
        b.ToTable("ReconciliationJobRuns");
        b.HasKey(x => x.Id);

        b.Property(x => x.StartedAtUtc).IsRequired();
        b.Property(x => x.EndedAtUtc);
        b.Property(x => x.DurationMs);
        b.Property(x => x.Success).IsRequired();
        b.Property(x => x.InProgress).IsRequired();
        b.Property(x => x.WasSkipped).IsRequired();
        b.Property(x => x.TriggerSource).HasMaxLength(24).IsRequired();
        b.Property(x => x.TriggeredByUserId);
        b.Property(x => x.WindowFromUtc).IsRequired();
        b.Property(x => x.WindowToUtc).IsRequired();
        b.Property(x => x.DraftSlaDays).IsRequired();
        b.Property(x => x.ApprovedSlaDays).IsRequired();
        b.Property(x => x.CandidatePayouts).IsRequired();
        b.Property(x => x.StaleDraftCount).IsRequired();
        b.Property(x => x.StaleApprovedCount).IsRequired();
        b.Property(x => x.PartialPaidCount).IsRequired();
        b.Property(x => x.PaidWithoutReferenceCount).IsRequired();
        b.Property(x => x.OverlapPairCount).IsRequired();
        b.Property(x => x.ReversedCount).IsRequired();
        b.Property(x => x.InsightsCount).IsRequired();
        b.Property(x => x.ReversalRatePercent).HasColumnType("decimal(9,2)");
        b.Property(x => x.PartialGapTotal).HasColumnType("decimal(18,2)");
        b.Property(x => x.SkipReason).HasMaxLength(320);
        b.Property(x => x.Error).HasMaxLength(1200);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => x.StartedAtUtc);
        b.HasIndex(x => new { x.Success, x.StartedAtUtc });
        b.HasIndex(x => new { x.WasSkipped, x.StartedAtUtc });
        b.HasIndex(x => x.InProgress)
            .HasFilter("[InProgress] = 1")
            .IsUnique()
            .HasDatabaseName("IX_ReconciliationJobRuns_InProgressUnique");
    }
}
