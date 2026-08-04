using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/stores/{slug}/categories")]
public class PublicCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicCategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.IsActive && s.Slug == slug);
        if (store is null) return NotFound();

        var categories = await _db.Categories.Where(c => c.StoreId == store.Id)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new PublicCategoryDto(c.Id, c.Name, c.DisplayOrder, c.Products.Count(p => p.IsAvailable)))
            .ToListAsync();
        return Ok(categories);
    }
}
