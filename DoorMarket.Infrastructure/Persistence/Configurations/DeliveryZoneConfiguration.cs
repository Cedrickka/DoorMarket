using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> b)
    {
        b.ToTable("DeliveryZones");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();

        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Country).HasMaxLength(8).IsRequired();
        b.Property(x => x.StateCode).HasMaxLength(8);
        b.Property(x => x.FeeUsd).HasColumnType("decimal(18,2)");
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}
