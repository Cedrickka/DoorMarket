using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.ToTable("Coupons");

        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.StartsAtUtc, x.EndsAtUtc });
        b.HasIndex(x => x.ScopeType);

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(400);

        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        b.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.MaxDiscountAmount).HasColumnType("decimal(18,2)");
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.MinSubtotal).HasColumnType("decimal(18,2)");

        b.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        b.Property(x => x.ScopeType).HasMaxLength(20).IsRequired();
        b.Property(x => x.ScopeCity).HasMaxLength(100);
        b.Property(x => x.ScopeCountry).HasMaxLength(100);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(x => x.ScopeShopId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.ScopeCategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
