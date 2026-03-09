using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class MarketingCampaignRunConfiguration : IEntityTypeConfiguration<MarketingCampaignRun>
{
    public void Configure(EntityTypeBuilder<MarketingCampaignRun> b)
    {
        b.ToTable("MarketingCampaignRuns");
        b.HasKey(x => x.Id);

        b.Property(x => x.RunType).HasMaxLength(24).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.TargetUsers).IsRequired();
        b.Property(x => x.SentCount).IsRequired();
        b.Property(x => x.FailedCount).IsRequired();
        b.Property(x => x.RevenueAttributed).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.DiscountCost).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.StartedAtUtc).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.CampaignId, x.StartedAtUtc });
        b.HasIndex(x => new { x.Status, x.StartedAtUtc });
    }
}
