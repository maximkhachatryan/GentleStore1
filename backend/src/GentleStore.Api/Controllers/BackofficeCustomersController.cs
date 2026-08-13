using GentleStore.Api.Auth;
using GentleStore.Api.Common;
using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/customers")]
public class BackofficeCustomersController : BackofficeControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly StorefrontOptions _storefront;

    public BackofficeCustomersController(AppDbContext db, ICurrentUser current, IOptions<StorefrontOptions> storefront)
    {
        _db = db;
        _current = current;
        _storefront = storefront.Value;
    }

    [HttpGet]
    public async Task<IActionResult> List(string? search, string? status)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var query = _db.Customers.Where(c => c.StoreId == storeId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            query = digits.Length > 0
                ? query.Where(c => EF.Functions.ILike(c.PhoneNormalized, $"%{digits}%")
                                   || (c.FullName != null && EF.Functions.ILike(c.FullName, $"%{term}%")))
                : query.Where(c => c.FullName != null && EF.Functions.ILike(c.FullName, $"%{term}%"));
        }

        var customers = await ProjectAsync(query.OrderByDescending(c => c.CreatedAt));

        // Status is derived from invites and sessions, so filtering happens after projection.
        if (!string.IsNullOrWhiteSpace(status))
            customers = customers.Where(c => c.Status == status.Trim().ToLowerInvariant()).ToList();

        return Ok(customers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var customers = await ProjectAsync(_db.Customers.Where(c => c.Id == id && c.StoreId == storeId));
        if (customers.Count == 0) return NotFound();

        var now = DateTime.UtcNow;

        var devices = await _db.CustomerSessions
            .Where(s => s.CustomerId == id && s.StoreId == storeId && s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new CustomerDeviceDto(s.Id, s.CreatedAt, s.LastSeenAt, s.UserAgent))
            .ToListAsync();

        var invites = await _db.CustomerInvites
            .Where(i => i.CustomerId == id && i.StoreId == storeId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new CustomerInviteDto(
                i.Id,
                i.RevokedAt != null ? "revoked"
                    : i.RedeemedAt != null ? "used"
                    : i.ExpiresAt <= now ? "expired"
                    : "pending",
                i.CreatedAt, i.ExpiresAt, i.RedeemedAt, i.RevokedAt, i.RedeemedUserAgent))
            .ToListAsync();

        return Ok(new CustomerDetailDto(customers[0], devices, invites));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (!TryReadPhone(req.Phone, out var phone, out var normalized, out var phoneError)) return phoneError!;

        if (await _db.Customers.AnyAsync(c => c.StoreId == storeId && c.PhoneNormalized == normalized))
            return Conflict(new { error = "A customer with this phone number already exists.", code = "phone_taken" });

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            Phone = phone,
            PhoneNormalized = normalized,
            FullName = Clean(req.FullName),
            Note = Clean(req.Note),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _current.UserId
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var created = await ProjectAsync(_db.Customers.Where(c => c.Id == customer.Id));
        return Created($"/api/backoffice/customers/{customer.Id}", created[0]);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCustomerRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (!TryReadPhone(req.Phone, out var phone, out var normalized, out var phoneError)) return phoneError!;

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (customer is null) return NotFound();

        if (normalized != customer.PhoneNormalized
            && await _db.Customers.AnyAsync(c => c.StoreId == storeId && c.PhoneNormalized == normalized))
            return Conflict(new { error = "A customer with this phone number already exists.", code = "phone_taken" });

        customer.Phone = phone;
        customer.PhoneNormalized = normalized;
        customer.FullName = Clean(req.FullName);
        customer.Note = Clean(req.Note);
        await _db.SaveChangesAsync();

        var updated = await ProjectAsync(_db.Customers.Where(c => c.Id == customer.Id));
        return Ok(updated[0]);
    }

    /// <summary>
    /// Mints a fresh single-use invite link. Any link that is still outstanding is revoked first,
    /// so "resend" cannot leave two working links in circulation. Already-redeemed links are left
    /// alone — the customer's existing devices keep working.
    /// </summary>
    [HttpPost("{id:guid}/invites")]
    public async Task<IActionResult> CreateInvite(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var customer = await _db.Customers
            .Include(c => c.Store)
            .FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (customer?.Store is null) return NotFound();
        if (customer.IsBlocked)
            return Conflict(new { error = "Unblock the customer before inviting them.", code = "customer_blocked" });

        var now = DateTime.UtcNow;
        await RevokePendingInvitesAsync(id, storeId, now);

        var secret = StorefrontTokens.NewSecret();
        var invite = new CustomerInvite
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            CustomerId = id,
            TokenHash = StorefrontTokens.Hash(secret),
            CreatedAt = now,
            ExpiresAt = now.AddDays(Math.Max(1, _storefront.InviteExpiryDays)),
            CreatedByUserId = _current.UserId
        };

        _db.CustomerInvites.Add(invite);
        await _db.SaveChangesAsync();

        var url = StorefrontLinks.Invite(_storefront.Url, customer.Store.Slug, secret);
        return Ok(new CustomerInviteLinkDto(invite.Id, url, invite.ExpiresAt));
    }

    /// <summary>Cancels an invite that was sent but never opened.</summary>
    [HttpDelete("{id:guid}/invites")]
    public async Task<IActionResult> RevokeInvites(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();
        if (!await _db.Customers.AnyAsync(c => c.Id == id && c.StoreId == storeId)) return NotFound();

        await RevokePendingInvitesAsync(id, storeId, DateTime.UtcNow);
        return NoContent();
    }

    /// <summary>Signs one browser out. That device needs a new invite link to get back in.</summary>
    [HttpPost("{id:guid}/devices/{deviceId:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(Guid id, Guid deviceId)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var session = await _db.CustomerSessions
            .FirstOrDefaultAsync(s => s.Id == deviceId && s.CustomerId == id && s.StoreId == storeId);
        if (session is null) return NotFound();

        session.RevokedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/block")]
    public Task<IActionResult> Block(Guid id) => SetBlocked(id, true);

    [HttpPost("{id:guid}/unblock")]
    public Task<IActionResult> Unblock(Guid id) => SetBlocked(id, false);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (customer is null) return NotFound();

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> SetBlocked(Guid id, bool blocked)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        if (customer is null) return NotFound();

        customer.IsBlocked = blocked;
        await _db.SaveChangesAsync();

        // Devices already signed in are refused at the gate as soon as the flag is set, but an
        // unopened link sitting in the customer's WhatsApp still has to be pulled.
        if (blocked) await RevokePendingInvitesAsync(id, storeId, DateTime.UtcNow);

        var projected = await ProjectAsync(_db.Customers.Where(c => c.Id == id));
        return Ok(projected[0]);
    }

    private Task RevokePendingInvitesAsync(Guid customerId, Guid storeId, DateTime now) =>
        _db.CustomerInvites
            .Where(i => i.CustomerId == customerId && i.StoreId == storeId
                        && i.RedeemedAt == null && i.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.RevokedAt, now));

    private static async Task<List<CustomerDto>> ProjectAsync(IQueryable<Customer> query)
    {
        var now = DateTime.UtcNow;

        var rows = await query
            .Select(c => new
            {
                c.Id,
                c.Phone,
                c.PhoneNormalized,
                c.FullName,
                c.Note,
                c.IsBlocked,
                c.CreatedAt,
                c.FirstActivatedAt,
                ActiveDeviceCount = c.Sessions.Count(s => s.RevokedAt == null),
                LastSeenAt = c.Sessions.Where(s => s.RevokedAt == null).Max(s => (DateTime?)s.LastSeenAt),
                PendingInviteExpiresAt = c.Invites
                    .Where(i => i.RedeemedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
                    .Max(i => (DateTime?)i.ExpiresAt),
                InviteCount = c.Invites.Count
            })
            .ToListAsync();

        return rows.Select(r => new CustomerDto(
            r.Id, r.Phone, r.PhoneNormalized, r.FullName, r.Note, r.IsBlocked,
            Status: r.IsBlocked ? "blocked"
                : r.ActiveDeviceCount > 0 ? "active"
                : r.PendingInviteExpiresAt is not null ? "invited"
                : r.InviteCount > 0 ? "expired"
                : "new",
            r.ActiveDeviceCount, r.PendingInviteExpiresAt, r.LastSeenAt, r.FirstActivatedAt, r.CreatedAt))
            .ToList();
    }

    private bool TryReadPhone(string? input, out string phone, out string normalized, out IActionResult? error)
    {
        phone = (input ?? string.Empty).Trim();
        error = null;

        if (PhoneNumbers.TryNormalize(phone, out normalized, out var problem)) return true;

        error = problem switch
        {
            PhoneNumbers.Problem.MissingCountryCode => BadRequest(new
            {
                error = "Enter the phone number in international format, including the country code.",
                code = "phone_missing_country_code"
            }),
            PhoneNumbers.Problem.TooLong => BadRequest(new
            {
                error = "That phone number is too long.",
                code = "phone_invalid"
            }),
            _ => BadRequest(new { error = "Phone is required.", code = "phone_required" })
        };
        return false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
