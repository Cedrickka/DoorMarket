using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ShopReviewHelpfulVoteConfiguration : IEntityTypeConfiguration<ShopReviewHelpfulVote>
{
    public void Configure(EntityTypeBuilder<ShopReviewHelpfulVote> b)
    {
        b.ToTable("ShopReviewHelpfulVotes");
        b.HasKey(x => x.Id);

        b.Property(x => x.IsHelpful).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.ShopReviewId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.ShopReviewId, x.IsHelpful });

        b.HasOne(x => x.User)
            .WithMany(u => u.ShopReviewHelpfulVotes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
