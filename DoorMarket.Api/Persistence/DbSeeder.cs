using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class DbSeeder
{
    public static async Task SeedAsync(DoorMarketDbContext db)
    {
        await db.Database.MigrateAsync();

        var hasher = new PasswordHasher<User>();

        async Task EnsureUser(string email, string password, UserRole role)
        {
            email = email.Trim().ToLowerInvariant();

            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user != null) return;

            user = new User
            {
                Email = email,
                Role = role,
                IsActive = true,
                EmailConfirmed = true
            };

            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        await EnsureUser("admin@doormarket.test", "Admin#12345", UserRole.Admin);
        await EnsureUser("shop@doormarket.test", "Shop#12345", UserRole.Shop);
        await EnsureUser("client@doormarket.test", "Client#12345", UserRole.Client);

        if (!await db.DeliveryZones.AnyAsync())
        {
            db.DeliveryZones.AddRange(
                new DeliveryZone { Code = "US-A", Name = "Zone A - Manhattan", Country = "US", StateCode = "NY", FeeUsd = 5m, IsActive = true },
                new DeliveryZone { Code = "US-B", Name = "Zone B - Brooklyn", Country = "US", StateCode = "NY", FeeUsd = 7m, IsActive = true },
                new DeliveryZone { Code = "US-C", Name = "Zone C - Queens", Country = "US", StateCode = "NY", FeeUsd = 9m, IsActive = true },
                new DeliveryZone { Code = "US-D", Name = "Zone D - Jersey City", Country = "US", StateCode = "NJ", FeeUsd = 11m, IsActive = true }
            );

            await db.SaveChangesAsync();
        }
    }
}
