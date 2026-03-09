using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class AbandonedCartEventConfiguration : IEntityTypeConfiguration<AbandonedCartEvent>
{
    public void Configure(EntityTypeBuilder<AbandonedCartEvent> b)
    {
        b.ToTable("AbandonedCartEvents");

        b.HasKey(x => x.Id);

        b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        b.Property(x => x.ItemCount).IsRequired();
        b.Property(x => x.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.LastCartActivityAtUtc).IsRequired();
        b.Property(x => x.DetectedAtUtc).IsRequired();
        b.Property(x => x.ExperimentGroup).HasMaxLength(16).HasDefaultValue("A").IsRequired();
        b.Property(x => x.ReminderStatus).HasMaxLength(24).IsRequired();
        b.Property(x => x.ReminderAttemptCount).IsRequired();
        b.Property(x => x.RecipientEmail).HasMaxLength(256);
        b.Property(x => x.SentChannels).HasMaxLength(64);
        b.Property(x => x.Error).HasMaxLength(1200);
        b.Property(x => x.ReminderSentAtUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => new { x.CartId, x.DetectedAtUtc });
        b.HasIndex(x => new { x.UserId, x.DetectedAtUtc });
        b.HasIndex(x => new { x.ReminderStatus, x.DetectedAtUtc });
        b.HasIndex(x => new { x.ExperimentGroup, x.DetectedAtUtc });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Cart)
            .WithMany()
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
