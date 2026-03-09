using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class NotificationIncidentAcknowledgementConfiguration : IEntityTypeConfiguration<NotificationIncidentAcknowledgement>
{
    public void Configure(EntityTypeBuilder<NotificationIncidentAcknowledgement> b)
    {
        b.ToTable("NotificationIncidentAcknowledgements");

        b.HasKey(x => x.Id);

        b.Property(x => x.OrderId);
        b.Property(x => x.LastLogId).IsRequired();
        b.Property(x => x.NotificationType).HasMaxLength(80).IsRequired();
        b.Property(x => x.Recipient).HasMaxLength(180).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.AcknowledgedBy).HasMaxLength(120).IsRequired();
        b.Property(x => x.AcknowledgedAtUtc).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.ReopenedBy).HasMaxLength(120);
        b.Property(x => x.ReopenedAtUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.OrderId, x.NotificationType, x.Recipient }).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.AcknowledgedAtUtc });
    }
}
