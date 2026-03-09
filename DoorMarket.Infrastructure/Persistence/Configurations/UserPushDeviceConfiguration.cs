using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public sealed class UserPushDeviceConfiguration : IEntityTypeConfiguration<UserPushDevice>
{
    public void Configure(EntityTypeBuilder<UserPushDevice> b)
    {
        b.ToTable("UserPushDevices");

        b.HasKey(x => x.Id);

        b.Property(x => x.Platform).HasMaxLength(16).IsRequired();
        b.Property(x => x.Token).HasMaxLength(512).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(128);
        b.Property(x => x.DeviceModel).HasMaxLength(128);
        b.Property(x => x.AppVersion).HasMaxLength(64);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.LastSeenAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.UserId, x.IsActive, x.LastSeenAtUtc });
        b.HasIndex(x => new { x.Platform, x.IsActive, x.LastSeenAtUtc });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

