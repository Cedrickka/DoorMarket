using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DoorMarket.Infrastructure.Seeding;

public static class AppSeeder
{
    public static async Task SeedUsersAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<DoorMarketDbContext>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<SeedUsersOptions>>().Value;

        var hasher = new PasswordHasher<User>();

        // Admin
        await UpsertUserAsync(db, hasher, opt.Admin, UserRole.Admin, ct);

        // Shop
        var shopUser = await UpsertUserAsync(db, hasher, opt.Shop, UserRole.Shop, ct);

        // Client
        await UpsertUserAsync(db, hasher, opt.Client, UserRole.Client, ct);

        // Optionnel : créer une boutique si ton entité Shop existe et que tu veux tester "mine"
        // -> Je te laisse 2 lignes à décommenter quand tu me montres ton entity Shop.
        // if (shopUser.Shop is null) { ... }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<User> UpsertUserAsync(
        DoorMarketDbContext db,
        PasswordHasher<User> hasher,
        SeedUser seed,
        UserRole role,
        CancellationToken ct)
    {
        var email = seed.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(seed.Phone) ? null : seed.Phone.Trim();

        var user = await db.Users
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                Phone = phone,
                Role = role,
                IsActive = true
            };

            user.PasswordHash = hasher.HashPassword(user, seed.Password);
            db.Users.Add(user);
            return user;
        }

        // Mise à jour soft si le user existe déjà
        user.Role = role;
        user.IsActive = true;

        if (phone is not null)
            user.Phone = phone;

        // Si tu veux forcer le reset du mot de passe à chaque run DEV, décommente :
        // user.PasswordHash = hasher.HashPassword(user, seed.Password);

        return user;
    }
}
