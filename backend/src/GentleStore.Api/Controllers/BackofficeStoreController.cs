using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/store")]
public class BackofficeStoreController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeStoreController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var s = await _db.Stores.FirstOrDefaultAsync(x => x.Id == storeId);
        return s is null ? NotFound() : Ok(ToDto(s));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateStoreProfileRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        var s = await _db.Stores.FirstOrDefaultAsync(x => x.Id == storeId);
        if (s is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Phone)) return BadRequest(new { error = "Phone is required." });

        if (!string.IsNullOrWhiteSpace(req.StorefrontAccess))
        {
            if (!Enum.TryParse<StorefrontAccessMode>(req.StorefrontAccess, ignoreCase: true, out var access))
                return BadRequest(new { error = "Unknown storefront access mode." });
            s.StorefrontAccess = access;
        }

        s.Name = req.Name.Trim();
        s.Phone = req.Phone.Trim();
        s.LogoUrl = string.IsNullOrWhiteSpace(req.LogoUrl) ? null : req.LogoUrl!.Trim();
        s.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim();
        s.Currency = string.IsNullOrWhiteSpace(req.Currency) ? s.Currency : req.Currency.Trim().ToUpper();

        await _db.SaveChangesAsync();
        return Ok(ToDto(s));
    }

    private static StoreProfileDto ToDto(Store s) => new(
        s.Id, s.Name, s.Slug, s.Phone, s.LogoUrl, s.Description, s.Currency, s.IsActive,
        s.StorefrontAccess.ToString());
}
