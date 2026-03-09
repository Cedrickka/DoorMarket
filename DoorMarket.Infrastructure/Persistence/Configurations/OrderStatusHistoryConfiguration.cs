using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("OrderStatusHistories");

        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });

        b.Property(x => x.OldStatus).HasMaxLength(30).IsRequired();
        b.Property(x => x.NewStatus).HasMaxLength(30).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.ChangedAtUtc).IsRequired();
    }
}
