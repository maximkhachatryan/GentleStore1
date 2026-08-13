using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/public/stores/{slug}/categories")]
public class PublicCategoriesController : PublicStoreControllerBase
{
    private readonly AppDbContext _db;

    public PublicCategoriesController(AppDbContext db, IStorefrontGate gate) : base(gate) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var categories = await _db.Categories.Where(c => c.StoreId == store!.Id)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new PublicCategoryDto(c.Id, c.Name, c.DisplayOrder, c.Products.Count(p => p.IsAvailable)))
            .ToListAsync();
        return Ok(categories);
    }
}
