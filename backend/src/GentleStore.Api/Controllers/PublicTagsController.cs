using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/public/stores/{slug}/tags")]
public class PublicTagsController : PublicStoreControllerBase
{
    private readonly AppDbContext _db;

    public PublicTagsController(AppDbContext db, IStorefrontGate gate) : base(gate) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var tags = await _db.Tags.Where(t => t.StoreId == store!.Id && t.ProductTags.Any(pt => pt.Product.IsAvailable))
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new PublicTagDto(t.Id, t.Name))
            .ToListAsync();
        return Ok(tags);
    }
}
