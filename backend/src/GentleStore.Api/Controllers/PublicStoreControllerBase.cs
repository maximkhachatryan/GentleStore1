using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.Controllers;

/// <summary>
/// Base for the anonymous storefront endpoints. Every one of them is addressed by store slug and
/// must respect that store's access mode, so resolving the tenant and enforcing the gate happen
/// together in one place.
/// </summary>
[ApiController]
[AllowAnonymous]
public abstract class PublicStoreControllerBase : ControllerBase
{
    /// <summary>Returned to the storefront so it can tell "private store" apart from "no such store".</summary>
    public const string InviteRequiredCode = "invite_required";

    protected PublicStoreControllerBase(IStorefrontGate gate) => Gate = gate;

    protected IStorefrontGate Gate { get; }

    /// <summary>
    /// Resolves <paramref name="slug"/> to an active store and identifies the customer behind the
    /// request, if any. When <c>Error</c> is non-null it is the response to return unchanged: 404
    /// for an unknown store, 403 for an invite-only store this browser has not unlocked.
    /// </summary>
    protected async Task<(Store? Store, StorefrontVisitor? Visitor, IActionResult? Error)> OpenStorefrontAsync(string slug)
    {
        var access = await Gate.AuthorizeAsync(slug, HttpContext.RequestAborted);

        return access.Denial switch
        {
            StorefrontDenial.StoreNotFound => (null, null, NotFound()),
            StorefrontDenial.InviteRequired => (null, null,
                StatusCode(StatusCodes.Status403Forbidden, new { code = InviteRequiredCode })),
            _ => (access.Store, access.Visitor, null)
        };
    }
}
