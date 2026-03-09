using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class MarketingBannerConfiguration : IEntityTypeConfiguration<MarketingBanner>
{
    public void Configure(EntityTypeBuilder<MarketingBanner> b)
    {
        b.ToTable("MarketingBanners");

        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Subtitle).HasMaxLength(500);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.TargetUrl).HasMaxLength(500);
        b.Property(x => x.Language).HasMaxLength(10);
        b.Property(x => x.City).HasMaxLength(120);
        b.Property(x => x.Zone).HasMaxLength(120);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.IsActive, x.StartAtUtc, x.EndAtUtc });
    }
}
