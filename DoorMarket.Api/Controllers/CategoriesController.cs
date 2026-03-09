using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.Interfaces.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    // PUBLIC
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await _categories.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Get(Guid id, CancellationToken ct)
    {
        var cat = await _categories.GetByIdAsync(id, ct);
        return cat is null ? NotFound() : Ok(cat);
    }

    // ADMIN
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
[HttpPost]
public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryCreateRequest req, CancellationToken ct)
        => Ok(await _categories.CreateAsync(req, ct));

[Authorize(Roles = "Admin,SuperAdmin,3,4")]
[HttpPut("{id:guid}")]
public async Task<ActionResult<CategoryDto>> Update(Guid id, [FromBody] CategoryUpdateRequest req, CancellationToken ct)
        => Ok(await _categories.UpdateAsync(id, req, ct));

[Authorize(Roles = "Admin,SuperAdmin,3,4")]
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
