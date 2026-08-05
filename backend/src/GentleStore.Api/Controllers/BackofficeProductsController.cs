using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/products")]
public class BackofficeProductsController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeProductsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid? categoryId, string? search)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var query = _db.Products.Where(p => p.StoreId == storeId);
        if (categoryId is not null) query = query.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));

        var products = await query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .ToListAsync();

        return Ok(products.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var product = await LoadProduct(storeId, id);
        return product is null ? NotFound() : Ok(ToDto(product));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var validation = await ValidateProduct(storeId, req.CategoryId, req.Name, req.Price);
        if (validation is not null) return validation;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            CategoryId = req.CategoryId,
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim(),
            Price = req.Price,
            IsAvailable = req.IsAvailable,
            DisplayOrder = req.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };
        await ApplyTags(storeId, product, req.TagIds);

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var created = await LoadProduct(storeId, product.Id);
        return Created($"/api/backoffice/products/{product.Id}", ToDto(created!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var product = await LoadProduct(storeId, id);
        if (product is null) return NotFound();
        var validation = await ValidateProduct(storeId, req.CategoryId, req.Name, req.Price);
        if (validation is not null) return validation;

        product.CategoryId = req.CategoryId;
        product.Name = req.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim();
        product.Price = req.Price;
        product.IsAvailable = req.IsAvailable;
        product.DisplayOrder = req.DisplayOrder;

        product.ProductTags.Clear();
        await ApplyTags(storeId, product, req.TagIds);

        await _db.SaveChangesAsync();
        var updated = await LoadProduct(storeId, product.Id);
        return Ok(ToDto(updated!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
        if (product is null) return NotFound();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> AddImage(Guid id, AddProductImageRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (string.IsNullOrWhiteSpace(req.ImageUrl)) return BadRequest(new { error = "Image URL is required." });
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
        if (product is null) return NotFound();

        var order = req.DisplayOrder ?? (product.Images.Count == 0 ? 1 : product.Images.Max(i => i.DisplayOrder) + 1);
        var image = new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, ImageUrl = req.ImageUrl.Trim(), DisplayOrder = order };
        _db.ProductImages.Add(image);
        await _db.SaveChangesAsync();
        return Created($"/api/backoffice/products/{id}/images/{image.Id}", new ProductImageDto(image.Id, image.ImageUrl, image.DisplayOrder));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id && i.Product.StoreId == storeId);
        if (image is null) return NotFound();
        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Product?> LoadProduct(Guid storeId, Guid id) =>
        await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

    private async Task<IActionResult?> ValidateProduct(Guid storeId, Guid categoryId, string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "Name is required." });
        if (price < 0) return BadRequest(new { error = "Price cannot be negative." });
        if (!await _db.Categories.AnyAsync(c => c.Id == categoryId && c.StoreId == storeId))
            return BadRequest(new { error = "Category does not belong to your store." });
        return null;
    }

    private async Task ApplyTags(Guid storeId, Product product, List<Guid>? tagIds)
    {
        if (tagIds is null || tagIds.Count == 0) return;
        var validTagIds = await _db.Tags.Where(t => t.StoreId == storeId && tagIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        foreach (var tagId in validTagIds)
            product.ProductTags.Add(new ProductTag { ProductId = product.Id, TagId = tagId });
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.CategoryId, p.Category?.Name ?? string.Empty, p.Name, p.Description,
        p.Price, p.IsAvailable, p.DisplayOrder,
        p.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.DisplayOrder)).ToList(),
        p.ProductTags.Select(pt => new ProductTagDto(pt.TagId, pt.Tag.Name)).ToList(),
        p.CreatedAt);
}
