using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);

        b.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.PlatformFeeAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.PlatformFeeMode).HasMaxLength(10).IsRequired().HasDefaultValue("Flat");
        b.Property(x => x.PlatformFeePercent).HasColumnType("decimal(9,4)");
        b.Property(x => x.PromotionPrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PromotionStartUtc);
        b.Property(x => x.PromotionEndUtc);

        b.HasIndex(x => new { x.ShopId, x.IsActive });

        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.Property(x => x.MainImageUrl).HasMaxLength(500);
    }
}
