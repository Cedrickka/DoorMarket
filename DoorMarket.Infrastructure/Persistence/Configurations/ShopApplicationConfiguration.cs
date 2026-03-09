using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopApplicationConfiguration : IEntityTypeConfiguration<ShopApplication>
{
    public void Configure(EntityTypeBuilder<ShopApplication> b)
    {
        b.ToTable("ShopApplications");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.CountryTag).HasMaxLength(10).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();

        b.Property(x => x.ReviewNote).HasMaxLength(800);

        // 1 candidature par user (V1)
        b.HasIndex(x => x.OwnerUserId).IsUnique();

        b.HasMany(x => x.Documents)
            .WithOne(d => d.ShopApplication)
            .HasForeignKey(d => d.ShopApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
