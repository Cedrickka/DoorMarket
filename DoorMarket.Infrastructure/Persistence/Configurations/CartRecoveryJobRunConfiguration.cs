using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class CartRecoveryJobRunConfiguration : IEntityTypeConfiguration<CartRecoveryJobRun>
{
    public void Configure(EntityTypeBuilder<CartRecoveryJobRun> b)
    {
        b.ToTable("CartRecoveryJobRuns");

        b.HasKey(x => x.Id);

        b.Property(x => x.StartedAtUtc).IsRequired();
        b.Property(x => x.EndedAtUtc).IsRequired();
        b.Property(x => x.DurationMs).IsRequired();
        b.Property(x => x.Success).IsRequired();
        b.Property(x => x.CandidatesScanned).IsRequired();
        b.Property(x => x.EventsCreated).IsRequired();
        b.Property(x => x.RemindersSent).IsRequired();
        b.Property(x => x.RemindersFailed).IsRequired();
        b.Property(x => x.AntiSpamSkipped).IsRequired();
        b.Property(x => x.ConvertedSkipped).IsRequired();
        b.Property(x => x.Error).HasMaxLength(1200);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => x.StartedAtUtc);
        b.HasIndex(x => new { x.Success, x.StartedAtUtc });
    }
}

