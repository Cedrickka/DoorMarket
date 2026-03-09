using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopPayoutConfiguration : IEntityTypeConfiguration<ShopPayout>
{
    public void Configure(EntityTypeBuilder<ShopPayout> b)
    {
        b.ToTable("ShopPayouts");

        b.HasKey(x => x.Id);

        b.Property(x => x.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        b.Property(x => x.PeriodStartUtc).IsRequired();
        b.Property(x => x.PeriodEndUtc).IsRequired();

        b.Property(x => x.GrossSalesItems).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.PlatformFee).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.NetToPay).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.DeliveryRevenue).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Draft");
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.Property(x => x.ReversalReason).HasMaxLength(500);
        b.Property(x => x.Reference).HasMaxLength(200);

        b.HasIndex(x => new { x.ShopId, x.PeriodStartUtc, x.PeriodEndUtc, x.Currency });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.ShopId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasMany(x => x.StatusHistory)
            .WithOne(x => x.ShopPayout)
            .HasForeignKey(x => x.ShopPayoutId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
