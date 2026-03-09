using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> b)
    {
        b.ToTable("Shops");

        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.CountryTag).HasMaxLength(10).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.DeliveryBaseFeeUsd).HasColumnType("decimal(18,2)").HasDefaultValue(2m).IsRequired();
        b.Property(x => x.DeliveryPerKmUsd).HasColumnType("decimal(18,2)").HasDefaultValue(0.55m).IsRequired();

        b.HasIndex(x => new { x.CountryTag, x.City });

        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
