using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class MarketingCampaignConfiguration : IEntityTypeConfiguration<MarketingCampaign>
{
    public void Configure(EntityTypeBuilder<MarketingCampaign> b)
    {
        b.ToTable("MarketingCampaigns");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(180).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.ChannelEmail).IsRequired();
        b.Property(x => x.ChannelPush).IsRequired();
        b.Property(x => x.ChannelInApp).IsRequired();
        b.Property(x => x.MessageTitle).HasMaxLength(300);
        b.Property(x => x.MessageBody).HasMaxLength(4000);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.Status, x.StartAtUtc });
        b.HasIndex(x => x.SegmentId);
        b.HasIndex(x => x.CouponId);

        b.HasOne(x => x.Segment)
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Coupon)
            .WithMany()
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Runs)
            .WithOne(x => x.Campaign)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
