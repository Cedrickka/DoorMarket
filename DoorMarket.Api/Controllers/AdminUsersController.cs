using DoorMarket.Application.Common;
using DoorMarket.Application.DTOs.AdminUsers;
using DoorMarket.Application.Interfaces.AdminUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _svc;

    public AdminUsersController(IAdminUserService svc)
    {
        _svc = svc;
    }

    // GET api/admin/users?q=&role=&isActive=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Get([FromQuery] AdminUserQuery query, CancellationToken ct)
        => Ok(await _svc.GetUsersAsync(query, ct));

    // PATCH api/admin/users/{id}/role
    [HttpPatch("{id:guid}/role")]
    [Authorize(Roles = "SuperAdmin,4")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeUserRoleRequest req, CancellationToken ct)
    {
        await _svc.ChangeRoleAsync(id, req.Role, ct);
        return NoContent();
    }

    // PUT api/admin/users/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,4")]
    public async Task<ActionResult<AdminUserDto>> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => Ok(await _svc.UpdateUserAsync(id, req, ct));

    // POST api/admin/users/create-shop
    [HttpPost("create-shop")]
    public async Task<ActionResult<AdminUserDto>> CreateShop([FromBody] CreateShopUserRequest req, CancellationToken ct)
        => Ok(await _svc.CreateShopUserAsync(req, ct));

    // POST api/admin/users
    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
        => Ok(await _svc.CreateUserAsync(req, ct));

    // PATCH api/admin/users/{id}/password
    [HttpPatch("{id:guid}/password")]
    [Authorize(Roles = "SuperAdmin,4")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangeUserPasswordRequest req, CancellationToken ct)
    {
        await _svc.ChangePasswordAsync(id, req.NewPassword, ct);
        return NoContent();
    }

    // DELETE api/admin/users/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,4")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteUserAsync(id, ct);
        return NoContent();
    }

    // GET api/admin/users/export.csv
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] AdminUserQuery query, CancellationToken ct)
    {
        var csv = await _svc.ExportCsvAsync(query, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "users.csv");
    }
}
