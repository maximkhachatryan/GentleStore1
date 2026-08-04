using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/tags")]
public class BackofficeTagsController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeTagsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var tags = await _db.Tags.Where(t => t.StoreId == storeId)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.DisplayOrder))
            .ToListAsync();
        return Ok(tags);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTagRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
        var name = req.Name.Trim();
        if (await _db.Tags.AnyAsync(t => t.StoreId == storeId && t.Name.ToLower() == name.ToLower()))
            return Conflict(new { error = "A tag with this name already exists." });

        var tag = new Tag { Id = Guid.NewGuid(), StoreId = storeId, Name = name, DisplayOrder = req.DisplayOrder };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/tags/{tag.Id}", new TagDto(tag.Id, tag.Name, tag.DisplayOrder));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTagRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.StoreId == storeId);
        if (tag is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });

        tag.Name = req.Name.Trim();
        tag.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new TagDto(tag.Id, tag.Name, tag.DisplayOrder));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.StoreId == storeId);
        if (tag is null) return NotFound();
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
