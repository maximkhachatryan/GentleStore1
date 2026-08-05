using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/products/{productId:guid}/variants")]
public class BackofficeProductVariantsController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeProductVariantsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid productId)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (!await ProductExists(storeId, productId)) return NotFound();

        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .Include(v => v.Attributes)
            .OrderBy(v => v.DisplayOrder)
            .ToListAsync();
        return Ok(variants.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid productId, CreateProductVariantRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (!await ProductExists(storeId, productId)) return NotFound();
        if (req.Price < 0) return BadRequest(new { error = "Price cannot be negative." });

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Sku = string.IsNullOrWhiteSpace(req.Sku) ? null : req.Sku!.Trim(),
            Price = req.Price,
            IsAvailable = req.IsAvailable,
            DisplayOrder = req.DisplayOrder
        };
        await ApplyOptions(storeId, variant, req.OptionIds);

        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/products/{productId}/variants/{variant.Id}", ToDto(variant));
    }

    [HttpPut("{variantId:guid}")]
    public async Task<IActionResult> Update(Guid productId, Guid variantId, UpdateProductVariantRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (req.Price < 0) return BadRequest(new { error = "Price cannot be negative." });

        var variant = await _db.ProductVariants
            .Include(v => v.Attributes)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId && v.Product.StoreId == storeId);
        if (variant is null) return NotFound();

        variant.Sku = string.IsNullOrWhiteSpace(req.Sku) ? null : req.Sku!.Trim();
        variant.Price = req.Price;
        variant.IsAvailable = req.IsAvailable;
        variant.DisplayOrder = req.DisplayOrder;

        // Safe to clear+re-add: the variant row is not deleted, so no DB cascade races EF's attribute deletes.
        variant.Attributes.Clear();
        await ApplyOptions(storeId, variant, req.OptionIds);

        await _db.SaveChangesAsync();
        return Ok(ToDto(variant));
    }

    [HttpDelete("{variantId:guid}")]
    public async Task<IActionResult> Delete(Guid productId, Guid variantId)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId && v.Product.StoreId == storeId);
        if (variant is null) return NotFound();
        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Task<bool> ProductExists(Guid storeId, Guid productId) =>
        _db.Products.AnyAsync(p => p.Id == productId && p.StoreId == storeId);

    private async Task ApplyOptions(Guid storeId, ProductVariant variant, List<Guid>? optionIds)
    {
        var ids = (optionIds ?? new List<Guid>()).Distinct().ToList();
        if (ids.Count == 0) return;

        var options = await _db.VariantAttributeOptions
            .Include(o => o.Definition)
            .Where(o => o.Definition.StoreId == storeId && ids.Contains(o.Id))
            .ToListAsync();

        var usedDefinitions = new HashSet<Guid>();
        foreach (var optionId in ids)
        {
            var option = options.FirstOrDefault(o => o.Id == optionId);
            if (option is null) continue;
            // One attribute per definition (unique index on ProductVariantId + Name).
            if (!usedDefinitions.Add(option.VariantAttributeDefinitionId)) continue;
            variant.Attributes.Add(new VariantAttribute
            {
                Id = Guid.NewGuid(),
                ProductVariantId = variant.Id,
                VariantAttributeDefinitionId = option.VariantAttributeDefinitionId,
                VariantAttributeOptionId = option.Id,
                Name = option.Definition.Name,
                Value = option.Value
            });
        }
    }

    private static ProductVariantDto ToDto(ProductVariant v) => new(
        v.Id, v.Sku, v.Price, v.IsAvailable, v.DisplayOrder,
        v.Attributes.Select(a => new VariantAttributeDto(
            a.VariantAttributeDefinitionId, a.VariantAttributeOptionId, a.Name, a.Value)).ToList());
}
