using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class ShopPayoutStatusHistoryConfiguration : IEntityTypeConfiguration<ShopPayoutStatusHistory>
{
    public void Configure(EntityTypeBuilder<ShopPayoutStatusHistory> b)
    {
        b.ToTable("ShopPayoutStatusHistories");

        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ShopPayoutId, x.ChangedAtUtc });
        b.HasIndex(x => new { x.ShopPayoutId, x.NewStatus, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        b.Property(x => x.OldStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.NewStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.ChangedAtUtc).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
