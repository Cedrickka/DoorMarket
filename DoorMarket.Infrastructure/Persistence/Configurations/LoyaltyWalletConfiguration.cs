using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class LoyaltyWalletConfiguration : IEntityTypeConfiguration<LoyaltyWallet>
{
    public void Configure(EntityTypeBuilder<LoyaltyWallet> b)
    {
        b.ToTable("LoyaltyWallets");
        b.HasKey(x => x.Id);

        b.Property(x => x.PointsBalance).IsRequired();
        b.Property(x => x.LifetimePointsEarned).IsRequired();
        b.Property(x => x.LifetimePointsSpent).IsRequired();
        b.Property(x => x.MonetaryBalance).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.UserId).IsUnique();

        b.HasOne(x => x.User)
            .WithOne(x => x.LoyaltyWallet)
            .HasForeignKey<LoyaltyWallet>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Ledger)
            .WithOne(x => x.Wallet)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
