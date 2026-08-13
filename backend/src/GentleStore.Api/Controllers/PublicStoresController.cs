using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/public/stores")]
public class PublicStoresController : PublicStoreControllerBase
{
    private readonly AppDbContext _db;

    public PublicStoresController(AppDbContext db, IStorefrontGate gate) : base(gate) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(string? search)
    {
        // Invite-only stores stay out of the directory — being listed would defeat the point.
        var query = _db.Stores.Where(s => s.IsActive && s.StorefrontAccess == StorefrontAccessMode.Public);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search.Trim()}%"));

        var stores = await query
            .OrderBy(s => s.Name)
            .Select(s => new PublicStoreListItemDto(s.Slug, s.Name, s.LogoUrl, s.Description, s.Phone))
            .ToListAsync();
        return Ok(stores);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug)
    {
        var (store, visitor, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        return Ok(new PublicStoreDto(
            store!.Slug, store.Name, store.LogoUrl, store.Description, store.Phone, store.Currency,
            store.StorefrontAccess.ToString(),
            visitor is null ? null : new StorefrontVisitorDto(visitor.DisplayName, visitor.PhoneMasked)));
    }

    /// <summary>
    /// Gate status for a storefront. Deliberately readable without an invite: the welcome and
    /// "link already used" screens need the store's name, logo and phone to be of any use.
    /// </summary>
    [HttpGet("{slug}/access")]
    public async Task<IActionResult> Access(string slug)
    {
        var store = await Gate.FindStoreAsync(slug, HttpContext.RequestAborted);
        if (store is null) return NotFound();

        var visitor = await Gate.CurrentVisitorAsync(store, HttpContext.RequestAborted);

        return Ok(new StorefrontAccessDto(
            store.Slug,
            store.Name,
            store.LogoUrl,
            store.Phone,
            store.StorefrontAccess.ToString(),
            Unlocked: store.StorefrontAccess == StorefrontAccessMode.Public || visitor is not null,
            Visitor: visitor is null ? null : new StorefrontVisitorDto(visitor.DisplayName, visitor.PhoneMasked)));
    }

    /// <summary>
    /// Claims a personal invite link for the calling browser and drops the long-lived session
    /// cookie. Rate limited per IP — the tokens are unguessable, but nothing should be free to
    /// hammer.
    /// </summary>
    [HttpPost("{slug}/access/redeem")]
    [EnableRateLimiting(RateLimitPolicies.InviteRedeem)]
    public async Task<IActionResult> Redeem(string slug, RedeemInviteRequest req)
    {
        var store = await Gate.FindStoreAsync(slug, HttpContext.RequestAborted);
        if (store is null) return NotFound();

        var result = await Gate.RedeemAsync(store, req.Token ?? string.Empty, HttpContext.RequestAborted);
        var visitor = result.Visitor is null
            ? null
            : new StorefrontVisitorDto(result.Visitor.DisplayName, result.Visitor.PhoneMasked);
        var body = new RedeemInviteResultDto(ToStatusCode(result.Outcome), visitor);

        return result.Outcome switch
        {
            RedeemOutcome.Unlocked or RedeemOutcome.AlreadyUnlocked => Ok(body),
            RedeemOutcome.InvalidToken => NotFound(body),
            // 409: the link itself was fine, it just belongs to another browser now.
            RedeemOutcome.AlreadyUsed => Conflict(body),
            RedeemOutcome.CustomerBlocked => StatusCode(StatusCodes.Status403Forbidden, body),
            // 410: the link is permanently done with, retrying will not help.
            _ => StatusCode(StatusCodes.Status410Gone, body)
        };
    }

    /// <summary>Stable, snake_case codes the storefront maps to its own copy.</summary>
    private static string ToStatusCode(RedeemOutcome outcome) => outcome switch
    {
        RedeemOutcome.Unlocked => "unlocked",
        RedeemOutcome.AlreadyUnlocked => "already_unlocked",
        RedeemOutcome.AlreadyUsed => "already_used",
        RedeemOutcome.Expired => "expired",
        RedeemOutcome.Revoked => "revoked",
        RedeemOutcome.CustomerBlocked => "blocked",
        _ => "invalid"
    };
}
