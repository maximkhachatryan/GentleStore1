using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/categories")]
public class BackofficeCategoriesController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeCategoriesController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var items = await _db.Categories.Where(c => c.StoreId == storeId)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.DisplayOrder, c.Products.Count))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCategoryRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });

        var category = new Category { Id = Guid.NewGuid(), StoreId = storeId, Name = req.Name.Trim(), DisplayOrder = req.DisplayOrder };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/categories/{category.Id}", new CategoryDto(category.Id, category.Name, category.DisplayOrder, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (category is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });

        category.Name = req.Name.Trim();
        category.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(category.Id, category.Name, category.DisplayOrder, await _db.Products.CountAsync(p => p.CategoryId == category.Id)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (category is null) return NotFound();
        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            return Conflict(new { error = "Cannot delete a category that still has products." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
