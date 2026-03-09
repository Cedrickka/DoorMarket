using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class CheckoutAlertIncidentConfiguration : IEntityTypeConfiguration<CheckoutAlertIncident>
{
    public void Configure(EntityTypeBuilder<CheckoutAlertIncident> b)
    {
        b.ToTable("CheckoutAlertIncidents");
        b.HasKey(x => x.Id);

        b.Property(x => x.AlertCode).HasMaxLength(120).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(24).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(800).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(40);
        b.Property(x => x.ObservedValue).HasColumnType("decimal(18,2)");
        b.Property(x => x.ThresholdValue).HasColumnType("decimal(18,2)");
        b.Property(x => x.FirstTriggeredAtUtc).IsRequired();
        b.Property(x => x.LastTriggeredAtUtc).IsRequired();
        b.Property(x => x.WindowFromUtc).IsRequired();
        b.Property(x => x.WindowToUtc).IsRequired();
        b.Property(x => x.TriggerCount).IsRequired();
        b.Property(x => x.IsAcknowledged).IsRequired();
        b.Property(x => x.AcknowledgedAtUtc);
        b.Property(x => x.AcknowledgedBy).HasMaxLength(120);
        b.Property(x => x.AcknowledgementNote).HasMaxLength(600);
        b.Property(x => x.ResolvedAtUtc);
        b.Property(x => x.LastNotifiedAtUtc);
        b.Property(x => x.NotificationCount).IsRequired();
        b.Property(x => x.LastNotificationError).HasMaxLength(1200);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => new { x.AlertCode, x.Provider, x.ResolvedAtUtc });
        b.HasIndex(x => new { x.IsAcknowledged, x.ResolvedAtUtc, x.LastTriggeredAtUtc });
        b.HasIndex(x => new { x.Severity, x.ResolvedAtUtc, x.LastTriggeredAtUtc });
    }
}
