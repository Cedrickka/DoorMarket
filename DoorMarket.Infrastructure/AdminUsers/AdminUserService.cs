using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.AdminUsers;
using DoorMarket.Application.Interfaces.AdminUsers;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.AdminUsers;

public sealed class AdminUserService : IAdminUserService
{
    private readonly DoorMarketDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly PasswordHasher<User> _hasher = new();

    public AdminUserService(DoorMarketDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(AdminUserQuery query, CancellationToken ct)
    {
        if (!IsAdminOrSuperAdmin())
        {
            throw new InvalidOperationException("Acces refuse.");
        }

        var q = _db.Users.AsNoTracking().AsQueryable();

        if (!IsSuperAdmin())
        {
            q = q.Where(x => x.Role != UserRole.SuperAdmin);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var s = query.Q.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.Email.ToLower().Contains(s) ||
                (x.Phone != null && x.Phone.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (Enum.TryParse<UserRole>(query.Role, ignoreCase: true, out var r))
                q = q.Where(x => x.Role == r);
        }

        if (query.IsActive is not null)
            q = q.Where(x => x.IsActive == query.IsActive.Value);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 200);

        var total = await q.CountAsync(ct);

        // Jointure boutique (si tu as Shops)
        // On suppose: Shop.OwnerUserId -> User.Id
        var items = await (
            from u in q.OrderByDescending(x => x.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
            join s in _db.Shops.AsNoTracking() on u.Id equals s.OwnerUserId into sj
            from shop in sj.DefaultIfEmpty()
            select new AdminUserDto(
                u.Id,
                u.Email,
                u.Phone,
                u.Role.ToString(),
                u.IsActive,
                u.CreatedAtUtc,     // idem: adapte si besoin
                shop != null ? shop.Id : null,
                shop != null ? shop.Name : null
            )
        ).ToListAsync(ct);

        return new PagedResult<AdminUserDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task ChangeRoleAsync(Guid userId, string role, CancellationToken ct)
    {
        EnsureSuperAdmin();

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var r))
            throw new InvalidOperationException("Rôle invalide (Admin/Shop/Client).");

        if (r == UserRole.SuperAdmin)
            throw new InvalidOperationException("Rôle invalide (Admin/Shop/Client).");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) throw new InvalidOperationException("Utilisateur introuvable.");

        user.Role = r;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AdminUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest req, CancellationToken ct)
    {
        EnsureSuperAdmin();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) throw new InvalidOperationException("Utilisateur introuvable.");

        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
            throw new InvalidOperationException("Rôle invalide (Admin/Shop/Client).");

        if (role == UserRole.SuperAdmin && user.Role != UserRole.SuperAdmin)
            throw new InvalidOperationException("Rôle invalide (Admin/Shop/Client).");

        user.Role = role;
        user.IsActive = req.IsActive;
        user.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            null,
            null
        );
    }

    public async Task<AdminUserDto> CreateShopUserAsync(CreateShopUserRequest req, CancellationToken ct)
    {
        if (!IsAdminOrSuperAdmin())
            throw new InvalidOperationException("Acces refuse.");

        var created = await CreateUserAsync(
            new CreateUserRequest(req.Email, req.Password, req.Phone, UserRole.Shop.ToString()),
            ct);
        return created;
    }

    public async Task<AdminUserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (!IsAdminOrSuperAdmin())
            throw new InvalidOperationException("Acces refuse.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email obligatoire.");

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            throw new InvalidOperationException("Mot de passe trop court.");

        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role))
            throw new InvalidOperationException("Role invalide (Admin/Shop/Client).");

        if (role == UserRole.SuperAdmin)
            throw new InvalidOperationException("Role invalide (Admin/Shop/Client).");

        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) throw new InvalidOperationException("Email déjà utilisé.");

        var user = new User
        {
            Email = email,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Role = role,
            IsActive = true,
            EmailConfirmed = role != UserRole.Client
        };

        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new AdminUserDto(
            user.Id,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            null,
            null
        );
    }

    public async Task ChangePasswordAsync(Guid userId, string newPassword, CancellationToken ct)
    {
        EnsureSuperAdmin();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new InvalidOperationException("Nouveau mot de passe invalide (minimum 6 caractères).");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) throw new InvalidOperationException("Utilisateur introuvable.");

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct)
    {
        EnsureSuperAdmin();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return;
        }

        var hasShop = await _db.Shops.AnyAsync(x => x.OwnerUserId == userId, ct);
        if (hasShop)
            throw new InvalidOperationException("Impossible de supprimer: cet utilisateur possede une boutique.");

        var hasOrders = await _db.Orders.AnyAsync(x => x.UserId == userId, ct);
        if (hasOrders)
            throw new InvalidOperationException("Impossible de supprimer: cet utilisateur a des commandes. Desactivez-le plutot.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
    }

    private bool IsAdminOrSuperAdmin()
        => string.Equals(_current.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(_current.Role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);

    private bool IsSuperAdmin()
        => string.Equals(_current.Role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);

    private void EnsureSuperAdmin()
    {
        if (!IsSuperAdmin())
        {
            throw new InvalidOperationException("Acces reserve au Super Admin.");
        }
    }



    public async Task<string> ExportCsvAsync(AdminUserQuery query, CancellationToken ct)
    {
        // On exporte le même filtrage que GetUsersAsync, mais en mode "tout"
        query = query with { Page = 1, PageSize = 200000 };

        var result = await GetUsersAsync(query, ct);

        static string Esc(string? v)
        {
            v ??= "";
            v = v.Replace("\"", "\"\"");
            return $"\"{v}\"";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Email,Phone,Role,IsActive,CreatedAtUtc,ShopId,ShopName");

        foreach (var u in result.Items)
        {
            sb.AppendLine(string.Join(",",
                Esc(u.Id.ToString()),
                Esc(u.Email),
                Esc(u.Phone),
                Esc(u.Role),
                Esc(u.IsActive.ToString()),
                Esc(u.CreatedAtUtc.ToString("O")),
                Esc(u.ShopId?.ToString()),
                Esc(u.ShopName)
            ));
        }

        return sb.ToString();
    }
}
