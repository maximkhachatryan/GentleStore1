using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/stores/{slug}/tags")]
public class PublicTagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicTagsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.IsActive && s.Slug == slug);
        if (store is null) return NotFound();

        var tags = await _db.Tags.Where(t => t.StoreId == store.Id && t.ProductTags.Any(pt => pt.Product.IsAvailable))
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new PublicTagDto(t.Id, t.Name))
            .ToListAsync();
        return Ok(tags);
    }
}
