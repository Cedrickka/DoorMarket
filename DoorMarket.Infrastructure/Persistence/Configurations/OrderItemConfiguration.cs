using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems");

        b.HasKey(x => x.Id);

        b.Property(x => x.Qty).IsRequired();
        b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.UnitPriceAtPurchase).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.PlatformFeeAtPurchase).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.LineTotal).HasColumnType("decimal(18,2)").IsRequired();


        b.HasIndex(x => new { x.OrderId, x.ProductId });

        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
