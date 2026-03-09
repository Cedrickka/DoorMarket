using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopReviewConfiguration : IEntityTypeConfiguration<ShopReview>
{
    public void Configure(EntityTypeBuilder<ShopReview> b)
    {
        b.ToTable("ShopReviews");

        b.HasKey(x => x.Id);

        b.Property(x => x.Rating).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.PhotoUrlsJson).HasMaxLength(4000);
        b.Property(x => x.VideoUrlsJson).HasMaxLength(4000);
        b.Property(x => x.HelpfulCount).IsRequired();
        b.Property(x => x.IsVerifiedPurchase).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });

        b.HasOne(x => x.Shop)
            .WithMany(s => s.Reviews)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany(u => u.ShopReviews)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.HelpfulVotes)
            .WithOne(x => x.ShopReview)
            .HasForeignKey(x => x.ShopReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
