using DoorMarket.Application.DTOs.Categories;
using DoorMarket.Application.Interfaces.Categories;
using DoorMarket.Domain.Entities;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoorMarket.Infrastructure.Categories;

public class CategoryService : ICategoryService
{
    private readonly DoorMarketDbContext _db;

    public CategoryService(DoorMarketDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Categories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CategoryDto(x.Id, x.Name, x.Slug, x.CreatedAtUtc, x.NameEn))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var x = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return x is null ? null : new CategoryDto(x.Id, x.Name, x.Slug, x.CreatedAtUtc, x.NameEn);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        var slug = req.Slug.Trim().ToLowerInvariant();
        var nameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn.Trim();

        var exists = await _db.Categories.AnyAsync(x => x.Slug == slug, ct);
        if (exists) throw new InvalidOperationException("Slug déjà utilisé.");

        var cat = new Category
        {
            Name = name,
            NameEn = nameEn,
            Slug = slug
        };

        _db.Categories.Add(cat);
        await _db.SaveChangesAsync(ct);

        return new CategoryDto(cat.Id, cat.Name, cat.Slug, cat.CreatedAtUtc, cat.NameEn);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryUpdateRequest req, CancellationToken ct)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("Catégorie introuvable.");

        var name = req.Name.Trim();
        var slug = req.Slug.Trim().ToLowerInvariant();
        var nameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn.Trim();

        var exists = await _db.Categories.AnyAsync(x => x.Slug == slug && x.Id != id, ct);
        if (exists) throw new InvalidOperationException("Slug déjà utilisé.");

        cat.Name = name;
        cat.NameEn = nameEn;
        cat.Slug = slug;
        cat.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new CategoryDto(cat.Id, cat.Name, cat.Slug, cat.CreatedAtUtc, cat.NameEn);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new InvalidOperationException("Catégorie introuvable.");

        // Option sécurité : empêcher suppression si produits liés
        var used = await _db.Products.AnyAsync(p => p.CategoryId == id, ct);
        if (used) throw new InvalidOperationException("Impossible: des produits utilisent cette catégorie.");

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync(ct);
    }
}
