using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoorMarket.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");

        b.HasKey(x => x.Id);

        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.EmailConfirmed).IsRequired().HasDefaultValue(false);
        b.Property(x => x.EmailVerificationCode).HasMaxLength(16);
        b.Property(x => x.EmailVerificationPurpose).HasMaxLength(32);

        b.Property(x => x.Phone).HasMaxLength(32);
        b.HasIndex(x => x.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
        b.Property(x => x.PendingPhone).HasMaxLength(32);
        b.Property(x => x.ProfileImageUrl).HasMaxLength(512);

        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.PendingPasswordHash).HasMaxLength(512);

        b.Property(x => x.Role).IsRequired();

        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasMany(x => x.RefreshTokens)
         .WithOne(x => x.User)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // 1-1 optionnel : User -> Shop
        b.HasOne(x => x.Shop)
         .WithOne(x => x.OwnerUser)
         .HasForeignKey<Shop>(x => x.OwnerUserId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
