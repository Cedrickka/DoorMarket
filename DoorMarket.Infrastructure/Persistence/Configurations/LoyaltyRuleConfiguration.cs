using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class LoyaltyRuleConfiguration : IEntityTypeConfiguration<LoyaltyRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyRule> b)
    {
        b.ToTable("LoyaltyRules");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.Priority).IsRequired();
        b.Property(x => x.EarnPointsPerUsd).HasColumnType("decimal(10,4)").IsRequired();
        b.Property(x => x.MinOrderAmountUsd).HasColumnType("decimal(18,2)");
        b.Property(x => x.RedeemValueUsdPerPoint).HasColumnType("decimal(18,6)").IsRequired();
        b.Property(x => x.MinRedeemPoints).IsRequired();
        b.Property(x => x.MaxRedeemPercentOfOrder).HasColumnType("decimal(9,2)").IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.IsActive, x.Priority });
    }
}
