using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Pricing;
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

        // Raw prices come back from the database and the sale rule runs here, so it lives in one
        // place (SalePricing) rather than being re-spelled as SQL.
        var rows = await query
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.SalePrice,
                p.SaleEndsAt,
                p.IsAvailable,
                PrimaryImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                p.CategoryId,
                Tags = p.ProductTags.OrderBy(pt => pt.Tag.DisplayOrder).Select(pt => pt.Tag.Name).ToList()
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var products = rows.Select(p =>
        {
            var price = SalePricing.Resolve(p.Price, p.SalePrice, p.SaleEndsAt, now);
            return new PublicProductListItemDto(
                p.Id, p.Name, price.Price, store.Currency, p.IsAvailable, p.PrimaryImageUrl,
                p.CategoryId, p.Tags, price.RegularPrice, price.OnSale ? p.SaleEndsAt : null);
        }).ToList();
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(string slug, Guid id)
    {
        var (resolved, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;
        var store = resolved!;

        var p = await _db.Products
            .Where(p => p.Id == id && p.StoreId == store.Id && p.IsAvailable)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.SalePrice,
                p.SaleEndsAt,
                p.IsAvailable,
                p.CategoryId,
                CategoryName = p.Category.Name,
                Images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PublicImageDto(i.ImageUrl, i.DisplayOrder)).ToList(),
                Tags = p.ProductTags.OrderBy(pt => pt.Tag.DisplayOrder).Select(pt => new PublicTagDto(pt.TagId, pt.Tag.Name)).ToList(),
                Variants = p.Variants.OrderBy(v => v.DisplayOrder).Select(v => new
                {
                    v.Id,
                    v.Sku,
                    v.Price,
                    v.SalePrice,
                    v.IsAvailable,
                    Attributes = v.Attributes.Select(a => new PublicVariantAttributeDto(a.Name, a.Value)).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (p is null) return NotFound();

        var now = DateTime.UtcNow;
        var price = SalePricing.Resolve(p.Price, p.SalePrice, p.SaleEndsAt, now);
        var variants = p.Variants.Select(v =>
        {
            var vp = SalePricing.Resolve(v.Price, v.SalePrice, p.SaleEndsAt, now);
            return new PublicVariantDto(v.Id, v.Sku, vp.Price!.Value, v.IsAvailable, v.Attributes, vp.RegularPrice);
        }).ToList();
        var anyOnSale = price.OnSale || variants.Any(v => v.RegularPrice is not null);

        return Ok(new PublicProductDto(
            p.Id, p.Name, p.Description, price.Price, store.Currency, p.IsAvailable,
            p.CategoryId, p.CategoryName, p.Images, p.Tags, variants,
            price.RegularPrice, anyOnSale ? p.SaleEndsAt : null));
    }
}
