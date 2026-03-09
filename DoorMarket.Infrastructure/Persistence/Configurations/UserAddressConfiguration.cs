using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> b)
    {
        b.ToTable("UserAddresses");
        b.HasKey(x => x.Id);

        b.Property(x => x.Label).HasMaxLength(50).IsRequired();        // ✅
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(50).IsRequired();

        b.Property(x => x.Country).HasMaxLength(100).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.District).HasMaxLength(120).IsRequired();
        b.Property(x => x.DeliveryZoneId);

        b.Property(x => x.Street).HasMaxLength(200).IsRequired();
        b.Property(x => x.Landmark).HasMaxLength(200);

        b.Property(x => x.IsDefault).HasDefaultValue(false);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.UserId, x.IsDefault });
        b.HasIndex(x => x.DeliveryZoneId);

        b.HasOne(x => x.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
