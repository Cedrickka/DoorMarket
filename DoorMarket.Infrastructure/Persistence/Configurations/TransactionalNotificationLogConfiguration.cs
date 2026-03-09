using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class TransactionalNotificationLogConfiguration : IEntityTypeConfiguration<TransactionalNotificationLog>
{
    public void Configure(EntityTypeBuilder<TransactionalNotificationLog> b)
    {
        b.ToTable("TransactionalNotificationLogs");

        b.HasKey(x => x.Id);

        b.Property(x => x.OrderId);
        b.Property(x => x.NotificationType).HasMaxLength(80).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(30).IsRequired();
        b.Property(x => x.Recipient).HasMaxLength(180).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(250).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.Error).HasMaxLength(1200);
        b.Property(x => x.AttemptedAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.AttemptedAtUtc);
        b.HasIndex(x => new { x.OrderId, x.AttemptedAtUtc });
        b.HasIndex(x => new { x.NotificationType, x.Status, x.AttemptedAtUtc });
    }
}
