using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class PromoAuditLogConfiguration : IEntityTypeConfiguration<PromoAuditLog>
{
    public void Configure(EntityTypeBuilder<PromoAuditLog> b)
    {
        b.ToTable("PromoAuditLogs");

        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.Source, x.CreatedAtUtc });
        b.HasIndex(x => x.OrderId);

        b.Property(x => x.Source).HasMaxLength(50).IsRequired();
        b.Property(x => x.PromoCode).HasMaxLength(50);
        b.Property(x => x.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.Discount).HasColumnType("decimal(18,2)").IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Applied).IsRequired();
        b.Property(x => x.Message).HasMaxLength(300).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
