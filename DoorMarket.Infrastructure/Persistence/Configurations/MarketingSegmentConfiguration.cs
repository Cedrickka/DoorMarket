using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class MarketingSegmentConfiguration : IEntityTypeConfiguration<MarketingSegment>
{
    public void Configure(EntityTypeBuilder<MarketingSegment> b)
    {
        b.ToTable("MarketingSegments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.CriteriaJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.IsSystem).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.IsSystem });
    }
}
