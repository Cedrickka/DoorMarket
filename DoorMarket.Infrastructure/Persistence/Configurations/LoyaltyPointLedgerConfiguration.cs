using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class LoyaltyPointLedgerConfiguration : IEntityTypeConfiguration<LoyaltyPointLedger>
{
    public void Configure(EntityTypeBuilder<LoyaltyPointLedger> b)
    {
        b.ToTable("LoyaltyPointLedgers");
        b.HasKey(x => x.Id);

        b.Property(x => x.EntryType).HasMaxLength(24).IsRequired();
        b.Property(x => x.DeltaPoints).IsRequired();
        b.Property(x => x.DeltaAmount).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.BalanceAfterPoints).IsRequired();
        b.Property(x => x.BalanceAfterAmount).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.SourceType).HasMaxLength(64);
        b.Property(x => x.SourceId).HasMaxLength(128);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.SourceType, x.SourceId });

        b.HasOne(x => x.User)
            .WithMany(x => x.LoyaltyPointLedgers)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
