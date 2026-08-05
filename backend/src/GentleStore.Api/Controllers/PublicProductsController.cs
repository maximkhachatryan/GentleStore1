using GentleStore.Api.Contracts;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/stores/{slug}/products")]
public class PublicProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug, Guid? categoryId, Guid? tagId, string? search)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.IsActive && s.Slug == slug);
        if (store is null) return NotFound();

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
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.IsActive && s.Slug == slug);
        if (store is null) return NotFound();

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
                p.ProductTags.OrderBy(pt => pt.Tag.DisplayOrder).Select(pt => new PublicTagDto(pt.TagId, pt.Tag.Name)).ToList()))
            .FirstOrDefaultAsync();

        return product is null ? NotFound() : Ok(product);
    }
}
