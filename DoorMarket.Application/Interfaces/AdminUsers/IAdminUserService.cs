using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.AdminUsers;

namespace DoorMarket.Application.Interfaces.AdminUsers;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(AdminUserQuery query, CancellationToken ct);
    Task<AdminUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest req, CancellationToken ct);
    Task ChangeRoleAsync(Guid userId, string role, CancellationToken ct);
    Task<AdminUserDto> CreateShopUserAsync(CreateShopUserRequest req, CancellationToken ct);
    Task<AdminUserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string newPassword, CancellationToken ct);
    Task DeleteUserAsync(Guid userId, CancellationToken ct);
    Task<string> ExportCsvAsync(AdminUserQuery query, CancellationToken ct);
}
