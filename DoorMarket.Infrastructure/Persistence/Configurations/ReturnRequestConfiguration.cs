using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> b)
    {
        b.ToTable("ReturnRequests");

        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(120).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.AdminNote).HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.OrderId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.SlaTargetAtUtc, x.Status });

        b.HasOne(x => x.Order)
            .WithMany(o => o.ReturnRequests)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany(u => u.ReturnRequests)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ReviewedByUser)
            .WithMany(u => u.ReviewedReturnRequests)
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.StatusHistory)
            .WithOne(x => x.ReturnRequest)
            .HasForeignKey(x => x.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
