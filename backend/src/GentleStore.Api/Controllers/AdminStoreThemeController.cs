using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Api.Theming;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

/// <summary>A store's storefront look. Set by a super admin only; store staff cannot change it.</summary>
[ApiController]
[Route("api/admin/stores/{id:guid}/theme")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminStoreThemeController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminStoreThemeController(AppDbContext db) => _db = db;

    /// <summary>The stored theme, the same filled in from its preset, and everything it can be made of.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        return store is null ? NotFound() : Ok(Describe(store.Theme));
    }

    [HttpPut]
    public async Task<IActionResult> Update(Guid id, StoreThemeRequest req)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is null) return NotFound();

        var (theme, code, error) = ThemeCatalog.Validate(req);
        if (theme is null) return BadRequest(new { code, error });

        store.Theme = theme;
        await _db.SaveChangesAsync();
        return Ok(Describe(store.Theme));
    }

    /// <summary>Back to the platform's own look.</summary>
    [HttpDelete]
    public async Task<IActionResult> Reset(Guid id)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is null) return NotFound();

        store.Theme = null;
        await _db.SaveChangesAsync();
        return Ok(Describe(null));
    }

    private static StoreThemeSettingsDto Describe(Domain.Entities.StoreTheme? theme) =>
        new(theme is null ? null : ThemeCatalog.ToRequest(theme), ThemeCatalog.Resolve(theme), ThemeCatalog.Catalog);
}
