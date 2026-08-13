using GentleStore.Api.Common;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace GentleStore.Api.Storefront;

public enum StorefrontDenial
{
    None,
    StoreNotFound,
    InviteRequired
}

public enum RedeemOutcome
{
    /// <summary>The link was claimed by this browser just now.</summary>
    Unlocked,

    /// <summary>This browser had already claimed this link — re-opening it is harmless.</summary>
    AlreadyUnlocked,

    /// <summary>Claimed by a different browser; the link is spent.</summary>
    AlreadyUsed,
    InvalidToken,
    Expired,
    Revoked,
    CustomerBlocked
}

/// <summary>The customer behind the current request, as much of them as the storefront may see.</summary>
public sealed record StorefrontVisitor(Guid CustomerId, Guid SessionId, string? DisplayName, string PhoneMasked);

public sealed record StorefrontAccessResult(Store? Store, StorefrontVisitor? Visitor, StorefrontDenial Denial);

public sealed record RedeemResult(RedeemOutcome Outcome, StorefrontVisitor? Visitor);

public interface IStorefrontGate
{
    /// <summary>Resolves the tenant behind a storefront slug and enforces its access mode.</summary>
    Task<StorefrontAccessResult> AuthorizeAsync(string slug, CancellationToken ct = default);

    /// <summary>Looks the store up without enforcing access — for the pre-login gate screen.</summary>
    Task<Store?> FindStoreAsync(string slug, CancellationToken ct = default);

    Task<StorefrontVisitor?> CurrentVisitorAsync(Store store, CancellationToken ct = default);

    /// <summary>Claims a one-time invite link for the calling browser.</summary>
    Task<RedeemResult> RedeemAsync(Store store, string token, CancellationToken ct = default);
}

public class StorefrontGate : IStorefrontGate
{
    /// <summary>How stale LastSeenAt may get before we spend a write refreshing it (and the cookie).</summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _accessor;
    private readonly StorefrontOptions _options;

    public StorefrontGate(AppDbContext db, IHttpContextAccessor accessor, IOptions<StorefrontOptions> options)
    {
        _db = db;
        _accessor = accessor;
        _options = options.Value;
    }

    public Task<Store?> FindStoreAsync(string slug, CancellationToken ct = default) =>
        _db.Stores.FirstOrDefaultAsync(s => s.IsActive && s.Slug == slug, ct);

    public async Task<StorefrontAccessResult> AuthorizeAsync(string slug, CancellationToken ct = default)
    {
        var store = await FindStoreAsync(slug, ct);
        if (store is null)
            return new StorefrontAccessResult(null, null, StorefrontDenial.StoreNotFound);

        var visitor = await CurrentVisitorAsync(store, ct);

        return store.StorefrontAccess == StorefrontAccessMode.InviteOnly && visitor is null
            ? new StorefrontAccessResult(store, null, StorefrontDenial.InviteRequired)
            : new StorefrontAccessResult(store, visitor, StorefrontDenial.None);
    }

    public async Task<StorefrontVisitor?> CurrentVisitorAsync(Store store, CancellationToken ct = default)
    {
        var cookie = ReadCookie(store.Id);
        if (cookie is null) return null;

        var session = await FindSessionAsync(store.Id, cookie, ct);
        if (session is null)
        {
            // The cookie names a session we will never honour again (revoked by the store, or
            // gone with a deleted customer): drop it so the browser stops sending it.
            DeleteCookie(store.Id);
            return null;
        }

        // Blocking, unlike revoking, is meant to be reversible — so the cookie stays put and this
        // device simply resumes working the moment the store unblocks the customer.
        if (session.Customer is null || session.Customer.IsBlocked) return null;

        await TouchAsync(session, ct);
        return ToVisitor(session);
    }

    public async Task<RedeemResult> RedeemAsync(Store store, string token, CancellationToken ct = default)
    {
        if (!StorefrontTokens.LooksLikeSecret(token))
            return new RedeemResult(RedeemOutcome.InvalidToken, null);

        var hash = StorefrontTokens.Hash(token);
        var invite = await _db.CustomerInvites
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.StoreId == store.Id, ct);

        if (invite?.Customer is null) return new RedeemResult(RedeemOutcome.InvalidToken, null);
        if (invite.Customer.IsBlocked) return new RedeemResult(RedeemOutcome.CustomerBlocked, null);
        if (invite.RevokedAt is not null) return new RedeemResult(RedeemOutcome.Revoked, null);

        var now = DateTime.UtcNow;
        var cookie = ReadCookie(store.Id);
        var presented = cookie is null ? null : await FindSessionAsync(store.Id, cookie, ct);

        if (invite.RedeemedAt is not null)
        {
            // Re-opening the same WhatsApp message from the browser that claimed it must keep
            // working; the same link arriving from anywhere else is a spent link.
            if (presented is not null && presented.CustomerInviteId == invite.Id)
            {
                await TouchAsync(presented, ct, force: true);
                return new RedeemResult(RedeemOutcome.AlreadyUnlocked, ToVisitor(presented));
            }

            return new RedeemResult(RedeemOutcome.AlreadyUsed, null);
        }

        if (invite.ExpiresAt <= now)
            return new RedeemResult(RedeemOutcome.Expired, null);

        var request = _accessor.HttpContext?.Request;
        var ip = _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = Truncate(request?.Headers[HeaderNames.UserAgent].ToString(), 400);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // First claim wins. The `RedeemedAt == null` predicate makes two browsers opening the
        // link at the same instant race-safe: exactly one UPDATE can match.
        var claimed = await _db.CustomerInvites
            .Where(i => i.Id == invite.Id && i.RedeemedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.RedeemedAt, now)
                .SetProperty(i => i.RedeemedIp, ip)
                .SetProperty(i => i.RedeemedUserAgent, userAgent), ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return new RedeemResult(RedeemOutcome.AlreadyUsed, null);
        }

        // This browser is about to throw away whichever session cookie it held for this store,
        // so retire that session instead of leaving an unreachable device in the store's list.
        if (presented is not null) presented.RevokedAt = now;

        var secret = StorefrontTokens.NewSecret();
        var session = new CustomerSession
        {
            Id = Guid.NewGuid(),
            StoreId = store.Id,
            CustomerId = invite.CustomerId,
            CustomerInviteId = invite.Id,
            TokenHash = StorefrontTokens.Hash(secret),
            CreatedAt = now,
            LastSeenAt = now,
            CreatedIp = Truncate(ip, 64),
            LastSeenIp = Truncate(ip, 64),
            UserAgent = userAgent
        };
        _db.CustomerSessions.Add(session);

        invite.Customer.FirstActivatedAt ??= now;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        session.Customer = invite.Customer;
        WriteCookie(store.Id, secret);
        return new RedeemResult(RedeemOutcome.Unlocked, ToVisitor(session));
    }

    private Task<CustomerSession?> FindSessionAsync(Guid storeId, string secret, CancellationToken ct)
    {
        if (!StorefrontTokens.LooksLikeSecret(secret)) return Task.FromResult<CustomerSession?>(null);

        var hash = StorefrontTokens.Hash(secret);
        return _db.CustomerSessions
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.StoreId == storeId && s.RevokedAt == null, ct);
    }

    /// <summary>
    /// Keeps the store's "last seen" column and the browser's cookie window fresh. Throttled so a
    /// page that fires four API calls does not write four times.
    /// </summary>
    private async Task TouchAsync(CustomerSession session, CancellationToken ct, bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - session.LastSeenAt < TouchInterval) return;

        session.LastSeenAt = now;
        session.LastSeenIp = Truncate(_accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), 64);
        await _db.SaveChangesAsync(ct);

        // Re-issuing the cookie slides its expiry forward, so a customer who keeps shopping
        // never runs into the browser's cap on persistent cookies.
        var cookie = ReadCookie(session.StoreId);
        if (cookie is not null) WriteCookie(session.StoreId, cookie);
    }

    private static StorefrontVisitor ToVisitor(CustomerSession session) => new(
        session.CustomerId,
        session.Id,
        string.IsNullOrWhiteSpace(session.Customer?.FullName) ? null : session.Customer!.FullName,
        PhoneNumbers.Mask(session.Customer?.PhoneNormalized ?? string.Empty));

    private string CookieName(Guid storeId) => $"{_options.SessionCookie.NamePrefix}_{storeId:N}";

    private string? ReadCookie(Guid storeId)
    {
        var value = _accessor.HttpContext?.Request.Cookies[CookieName(storeId)];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void WriteCookie(Guid storeId, string secret)
    {
        var context = _accessor.HttpContext;
        if (context is null || context.Response.HasStarted) return;

        var options = BuildCookieOptions(context);
        options.Expires = DateTimeOffset.UtcNow.AddDays(_options.SessionCookie.MaxAgeDays);
        context.Response.Cookies.Append(CookieName(storeId), secret, options);
    }

    private void DeleteCookie(Guid storeId)
    {
        var context = _accessor.HttpContext;
        if (context is null || context.Response.HasStarted) return;

        // Domain/Path/Secure/SameSite must match the original or the browser keeps the cookie.
        context.Response.Cookies.Delete(CookieName(storeId), BuildCookieOptions(context));
    }

    private CookieOptions BuildCookieOptions(HttpContext context)
    {
        var configured = _options.SessionCookie;
        var sameSite = Enum.TryParse<SameSiteMode>(configured.SameSite, ignoreCase: true, out var parsed)
            ? parsed
            : SameSiteMode.Lax;

        return new CookieOptions
        {
            // The storefront never needs to read this value, so keep it out of reach of any
            // script that manages to run on the page.
            HttpOnly = true,
            IsEssential = true,
            SameSite = sameSite,
            // SameSite=None is only honoured on secure cookies.
            Secure = sameSite == SameSiteMode.None || (configured.Secure ?? context.Request.IsHttps),
            Domain = string.IsNullOrWhiteSpace(configured.Domain) ? null : configured.Domain,
            Path = "/"
        };
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
