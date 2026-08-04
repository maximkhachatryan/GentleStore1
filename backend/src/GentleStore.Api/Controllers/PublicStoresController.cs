using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/stores")]
public class PublicStoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicStoresController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string? search)
    {
        var query = _db.Stores.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search.Trim()}%"));

        var stores = await query
            .OrderBy(s => s.Name)
            .Select(s => new PublicStoreListItemDto(s.Slug, s.Name, s.LogoUrl, s.Description, s.Phone))
            .ToListAsync();
        return Ok(stores);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var store = await _db.Stores.Where(s => s.IsActive && s.Slug == slug)
            .Select(s => new PublicStoreDto(s.Slug, s.Name, s.LogoUrl, s.Description, s.Phone, s.Currency))
            .FirstOrDefaultAsync();
        return store is null ? NotFound() : Ok(store);
    }
}
