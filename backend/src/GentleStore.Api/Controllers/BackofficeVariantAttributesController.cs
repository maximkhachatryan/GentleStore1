using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/variant-attributes")]
public class BackofficeVariantAttributesController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeVariantAttributesController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var definitions = await _db.VariantAttributeDefinitions
            .Where(d => d.StoreId == storeId)
            .Include(d => d.Options)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .ToListAsync();
        return Ok(definitions.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateVariantAttributeDefinitionRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
        if (await _db.VariantAttributeDefinitions.AnyAsync(d => d.StoreId == storeId && d.Name == req.Name.Trim()))
            return Conflict(new { error = "An attribute with this name already exists." });

        var definition = new VariantAttributeDefinition
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Name = req.Name.Trim(),
            DisplayOrder = req.DisplayOrder
        };
        _db.VariantAttributeDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/variant-attributes/{definition.Id}", ToDto(definition));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateVariantAttributeDefinitionRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var definition = await LoadDefinition(storeId, id);
        if (definition is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
        if (await _db.VariantAttributeDefinitions.AnyAsync(d => d.StoreId == storeId && d.Name == req.Name.Trim() && d.Id != id))
            return Conflict(new { error = "An attribute with this name already exists." });

        definition.Name = req.Name.Trim();
        definition.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(ToDto(definition));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var definition = await _db.VariantAttributeDefinitions.FirstOrDefaultAsync(d => d.Id == id && d.StoreId == storeId);
        if (definition is null) return NotFound();
        _db.VariantAttributeDefinitions.Remove(definition);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/options")]
    public async Task<IActionResult> AddOption(Guid id, CreateVariantAttributeOptionRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var definition = await LoadDefinition(storeId, id);
        if (definition is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Value)) return BadRequest(new { error = "Value is required." });
        if (definition.Options.Any(o => o.Value == req.Value.Trim()))
            return Conflict(new { error = "This value already exists for the attribute." });

        var option = new VariantAttributeOption
        {
            Id = Guid.NewGuid(),
            VariantAttributeDefinitionId = definition.Id,
            Value = req.Value.Trim(),
            DisplayOrder = req.DisplayOrder
        };
        _db.VariantAttributeOptions.Add(option);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/variant-attributes/{id}/options/{option.Id}",
            new VariantAttributeOptionDto(option.Id, option.Value, option.DisplayOrder));
    }

    [HttpPut("{id:guid}/options/{optionId:guid}")]
    public async Task<IActionResult> UpdateOption(Guid id, Guid optionId, UpdateVariantAttributeOptionRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var option = await _db.VariantAttributeOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.VariantAttributeDefinitionId == id && o.Definition.StoreId == storeId);
        if (option is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Value)) return BadRequest(new { error = "Value is required." });
        if (await _db.VariantAttributeOptions.AnyAsync(o => o.VariantAttributeDefinitionId == id && o.Value == req.Value.Trim() && o.Id != optionId))
            return Conflict(new { error = "This value already exists for the attribute." });

        option.Value = req.Value.Trim();
        option.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new VariantAttributeOptionDto(option.Id, option.Value, option.DisplayOrder));
    }

    [HttpDelete("{id:guid}/options/{optionId:guid}")]
    public async Task<IActionResult> DeleteOption(Guid id, Guid optionId)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var option = await _db.VariantAttributeOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.VariantAttributeDefinitionId == id && o.Definition.StoreId == storeId);
        if (option is null) return NotFound();
        _db.VariantAttributeOptions.Remove(option);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<VariantAttributeDefinition?> LoadDefinition(Guid storeId, Guid id) =>
        await _db.VariantAttributeDefinitions
            .Include(d => d.Options)
            .FirstOrDefaultAsync(d => d.Id == id && d.StoreId == storeId);

    private static VariantAttributeDefinitionDto ToDto(VariantAttributeDefinition d) => new(
        d.Id, d.Name, d.DisplayOrder,
        d.Options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.Value)
            .Select(o => new VariantAttributeOptionDto(o.Id, o.Value, o.DisplayOrder)).ToList());
}
