using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> b)
    {
        b.ToTable("CommissionRules");

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.ScopeType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.PlatformFeeMode).HasMaxLength(10).IsRequired();
        b.Property(x => x.PlatformFeeAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.PlatformFeePercent).HasColumnType("decimal(9,4)");
        b.Property(x => x.MinUnitPrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.MaxUnitPrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.Priority).HasDefaultValue(100);
        b.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(x => x.ScopeShopId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.ScopeCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ScopeProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.IsActive, x.ScopeType, x.Priority });
        b.HasIndex(x => new { x.ScopeShopId, x.ScopeCategoryId, x.ScopeProductId, x.IsActive });
        b.HasIndex(x => new { x.StartsAtUtc, x.EndsAtUtc });
    }
}
