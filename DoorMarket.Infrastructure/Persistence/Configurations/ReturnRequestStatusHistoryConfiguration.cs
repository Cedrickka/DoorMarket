using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class ReturnRequestStatusHistoryConfiguration : IEntityTypeConfiguration<ReturnRequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReturnRequestStatusHistory> b)
    {
        b.ToTable("ReturnRequestStatusHistories");

        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ReturnRequestId, x.ChangedAtUtc });

        b.Property(x => x.OldStatus).HasMaxLength(24).IsRequired();
        b.Property(x => x.NewStatus).HasMaxLength(24).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ChangedAtUtc).IsRequired();

        b.HasOne(x => x.ReturnRequest)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ChangedByUser)
            .WithMany(x => x.ReturnRequestStatusHistories)
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
