using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/public/stores/{slug}/products")]
public class PublicProductsController : PublicStoreControllerBase
{
    private readonly AppDbContext _db;

    public PublicProductsController(AppDbContext db, IStorefrontGate gate) : base(gate) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug, Guid? categoryId, Guid? tagId, string? search)
    {
        var (resolved, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;
        var store = resolved!;

        var query = _db.Products.Where(p => p.StoreId == store.Id && p.IsAvailable);
        if (categoryId is not null) query = query.Where(p => p.CategoryId == categoryId);
        if (tagId is not null) query = query.Where(p => p.ProductTags.Any(pt => pt.TagId == tagId));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));

        var products = await query
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .Select(p => new PublicProductListItemDto(
                p.Id,
                p.Name,
                p.Price,
                store.Currency,
                p.IsAvailable,
                p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                p.CategoryId,
                p.ProductTags.OrderBy(pt => pt.Tag.DisplayOrder).Select(pt => pt.Tag.Name).ToList()))
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(string slug, Guid id)
    {
        var (resolved, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;
        var store = resolved!;

        var product = await _db.Products
            .Where(p => p.Id == id && p.StoreId == store.Id && p.IsAvailable)
            .Select(p => new PublicProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                store.Currency,
                p.IsAvailable,
                p.CategoryId,
                p.Category.Name,
                p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PublicImageDto(i.ImageUrl, i.DisplayOrder)).ToList(),
                p.ProductTags.OrderBy(pt => pt.Tag.DisplayOrder).Select(pt => new PublicTagDto(pt.TagId, pt.Tag.Name)).ToList(),
                p.Variants.OrderBy(v => v.DisplayOrder).Select(v => new PublicVariantDto(
                    v.Id,
                    v.Sku,
                    v.Price,
                    v.IsAvailable,
                    v.Attributes.Select(a => new PublicVariantAttributeDto(a.Name, a.Value)).ToList())).ToList()))
            .FirstOrDefaultAsync();

        return product is null ? NotFound() : Ok(product);
    }
}
