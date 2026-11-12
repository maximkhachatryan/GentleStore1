using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Pricing;
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
            .Include(p => p.Variants).ThenInclude(v => v.Attributes)
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
        var validation = await ValidateProduct(storeId, req.CategoryId, req.Name, req.Price, req.SalePrice);
        if (validation is not null) return validation;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            CategoryId = req.CategoryId,
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim(),
            Price = req.Price,
            SalePrice = req.SalePrice,
            SaleEndsAt = req.SalePrice is null ? null : ToUtc(req.SaleEndsAt),
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
        var product = await _db.Products
            .Include(p => p.ProductTags)
            .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
        if (product is null) return NotFound();
        var validation = await ValidateProduct(storeId, req.CategoryId, req.Name, req.Price, req.SalePrice);
        if (validation is not null) return validation;

        product.CategoryId = req.CategoryId;
        product.Name = req.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim();
        product.Price = req.Price;
        product.SalePrice = req.SalePrice;
        // The end date is shared with the variants' sale prices, so it stays when only they are on sale.
        product.SaleEndsAt = ToUtc(req.SaleEndsAt);
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

    [HttpPost("sale")]
    public async Task<IActionResult> ApplySale(ApplySaleRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (req.Percent is < SalePricing.MinPercent or > SalePricing.MaxPercent)
            return BadRequest(new
            {
                error = $"Discount must be between {SalePricing.MinPercent}% and {SalePricing.MaxPercent}%.",
                code = "percent_invalid"
            });

        var endsAt = ToUtc(req.EndsAt);
        if (endsAt <= DateTime.UtcNow)
            return BadRequest(new { error = "The sale must end in the future.", code = "ends_in_past" });

        var products = await SaleScope(storeId, req.CategoryId, req.TagId);
        var changed = 0;
        foreach (var product in products)
        {
            product.SalePrice = product.Price is { } price ? SalePricing.Discount(price, req.Percent) : null;
            foreach (var variant in product.Variants)
                variant.SalePrice = SalePricing.Discount(variant.Price, req.Percent);

            var onSale = product.SalePrice is not null || product.Variants.Any(v => v.SalePrice is not null);
            product.SaleEndsAt = onSale ? endsAt : null;
            if (onSale) changed++;
        }

        await _db.SaveChangesAsync();
        return Ok(new SaleResultDto(changed));
    }

    [HttpPost("sale/end")]
    public async Task<IActionResult> EndSale(EndSaleRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var products = await SaleScope(storeId, req.CategoryId, req.TagId);
        var changed = 0;
        foreach (var product in products)
        {
            if (product.SalePrice is null && product.SaleEndsAt is null && product.Variants.All(v => v.SalePrice is null))
                continue;
            product.SalePrice = null;
            product.SaleEndsAt = null;
            foreach (var variant in product.Variants) variant.SalePrice = null;
            changed++;
        }

        await _db.SaveChangesAsync();
        return Ok(new SaleResultDto(changed));
    }

    private Task<List<Product>> SaleScope(Guid storeId, Guid? categoryId, Guid? tagId)
    {
        var query = _db.Products.Where(p => p.StoreId == storeId);
        if (categoryId is not null) query = query.Where(p => p.CategoryId == categoryId);
        if (tagId is not null) query = query.Where(p => p.ProductTags.Any(pt => pt.TagId == tagId));
        return query.Include(p => p.Variants).ToListAsync();
    }

    /// <summary>A date without a zone is taken as UTC; Npgsql refuses to store any other kind.</summary>
    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
    };

    private async Task<Product?> LoadProduct(Guid storeId, Guid id) =>
        await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Variants).ThenInclude(v => v.Attributes)
            .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

    private async Task<IActionResult?> ValidateProduct(
        Guid storeId, Guid categoryId, string name, decimal? price, decimal? salePrice)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "Name is required." });
        if (price < 0) return BadRequest(new { error = "Price cannot be negative." });
        if (salePrice is not null && (salePrice < 0 || price is null || salePrice >= price))
            return BadRequest(new { error = "Sale price must be below the regular price.", code = "sale_price_invalid" });
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
        p.Variants.OrderBy(v => v.DisplayOrder).Select(v => new ProductVariantDto(
            v.Id, v.Sku, v.Price, v.IsAvailable, v.DisplayOrder,
            v.Attributes.Select(a => new VariantAttributeDto(
                a.VariantAttributeDefinitionId, a.VariantAttributeOptionId, a.Name, a.Value)).ToList(),
            v.SalePrice)).ToList(),
        p.CreatedAt, p.SalePrice, p.SaleEndsAt);
}
