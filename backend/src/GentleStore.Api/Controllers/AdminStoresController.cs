using GentleStore.Api.Auth;
using GentleStore.Api.Common;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[Route("api/admin/stores")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminStoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminStoresController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var stores = await _db.Stores
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StoreListItemDto(
                s.Id, s.Name, s.Slug, s.Phone, s.LogoUrl, s.IsActive, s.Currency,
                s.Products.Count, s.Categories.Count, s.CreatedAt))
            .ToListAsync();
        return Ok(stores);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var s = await _db.Stores.FirstOrDefaultAsync(x => x.Id == id);
        return s is null
            ? NotFound()
            : Ok(new StoreDetailDto(s.Id, s.Name, s.Slug, s.Phone, s.LogoUrl, s.Description, s.Currency, s.IsActive, s.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStoreRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { error = "Phone is required." });

        var slug = await UniqueSlugAsync(string.IsNullOrWhiteSpace(req.Slug) ? req.Name : req.Slug!);

        var store = new Store
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Slug = slug,
            Phone = req.Phone.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(req.LogoUrl) ? null : req.LogoUrl!.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim(),
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency!.Trim().ToUpper(),
            IsActive = req.IsActive ?? true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Stores.Add(store);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/stores/{store.Id}",
            new StoreDetailDto(store.Id, store.Name, store.Slug, store.Phone, store.LogoUrl, store.Description, store.Currency, store.IsActive, store.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStoreRequest req)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(x => x.Id == id);
        if (store is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Phone)) return BadRequest(new { error = "Phone is required." });

        if (!string.IsNullOrWhiteSpace(req.Slug))
        {
            var desired = SlugHelper.Generate(req.Slug!);
            if (desired != store.Slug)
                store.Slug = await UniqueSlugAsync(desired, store.Id);
        }

        store.Name = req.Name.Trim();
        store.Phone = req.Phone.Trim();
        store.LogoUrl = string.IsNullOrWhiteSpace(req.LogoUrl) ? null : req.LogoUrl!.Trim();
        store.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim();
        store.Currency = string.IsNullOrWhiteSpace(req.Currency) ? store.Currency : req.Currency.Trim().ToUpper();
        store.IsActive = req.IsActive;

        await _db.SaveChangesAsync();
        return Ok(new StoreDetailDto(store.Id, store.Name, store.Slug, store.Phone, store.LogoUrl, store.Description, store.Currency, store.IsActive, store.CreatedAt));
    }

    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id) => SetActive(id, true);

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id) => SetActive(id, false);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(x => x.Id == id);
        if (store is null) return NotFound();
        _db.Stores.Remove(store);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetActive(Guid id, bool active)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(x => x.Id == id);
        if (store is null) return NotFound();
        store.IsActive = active;
        await _db.SaveChangesAsync();
        return Ok(new { store.Id, store.IsActive });
    }

    private async Task<string> UniqueSlugAsync(string desired, Guid? excludeId = null)
    {
        var baseSlug = SlugHelper.Generate(desired);
        var slug = baseSlug;
        var i = 2;
        while (await _db.Stores.AnyAsync(s => s.Slug == slug && s.Id != excludeId))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
