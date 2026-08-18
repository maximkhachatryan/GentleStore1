using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Infrastructure.Payments;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

/// <summary>
/// Switches online payments on for one tenant.
/// <para>
/// The tenant flag lives here; which providers the store uses, with which credentials, lives in
/// PaymentService against the same store id. This endpoint is the seam that keeps the two
/// consistent, and it always writes PaymentService first — a store whose flag says "customers must
/// pay" while no gateway is enrolled would block every checkout it has.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/stores/{id:guid}/payments")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminStorePaymentsController : ControllerBase
{
    private const string DefaultMode = "Test";

    private readonly AppDbContext _db;
    private readonly IPaymentClient _payments;

    public AdminStorePaymentsController(AppDbContext db, IPaymentClient payments)
    {
        _db = db;
        _payments = payments;
    }

    /// <summary>Current setting, plus every method this deployment could offer the store.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid id)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is null) return NotFound();

        if (!_payments.IsConfigured) return Ok(Unavailable(store, "payments_not_configured"));

        var gateways = await _payments.ListGatewaysAsync(HttpContext.RequestAborted);
        if (!gateways.IsSuccess)
            return Ok(Unavailable(store, gateways.Code ?? "payment_service_unavailable"));

        var accounts = await _payments.ListAccountsAsync(id, HttpContext.RequestAborted);
        return Ok(Describe(store, gateways.Value!, accounts.IsSuccess ? accounts.Value! : []));
    }

    /// <summary>
    /// Enrols or un-enrols the store. Enabling requires a credential reference and at least one
    /// method that can actually take the store's currency.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update(Guid id, UpdateStorePaymentsRequest req)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is null) return NotFound();

        if (!_payments.IsConfigured)
            return Fail("payments_not_configured", "This deployment has no payment service configured.");

        var gateways = await _payments.ListGatewaysAsync(HttpContext.RequestAborted);
        if (!gateways.IsSuccess) return Upstream(gateways.Code, gateways.Detail);

        var accounts = await _payments.ListAccountsAsync(id, HttpContext.RequestAborted);
        if (!accounts.IsSuccess) return Upstream(accounts.Code, accounts.Detail);

        // Turning payments off disables the enrolments but never deletes them, so the store keeps
        // the ability to refund what it has already taken.
        if (!req.Enabled)
        {
            foreach (var account in accounts.Value!.Where(a => a.IsEnabled))
            {
                var disabled = await _payments.DisableAccountAsync(id, account.Id, HttpContext.RequestAborted);
                if (!disabled.IsSuccess) return Upstream(disabled.Code, disabled.Detail);
            }

            store.PaymentsEnabled = false;
            await _db.SaveChangesAsync();
            return Ok(Describe(store, gateways.Value!, await ReloadAsync(id)));
        }

        var credentialRef = req.CredentialRef?.Trim();
        if (string.IsNullOrWhiteSpace(credentialRef))
            return Fail("credential_ref_required", "Point the store at its own credentials in the payment service.");

        var mode = string.IsNullOrWhiteSpace(req.Mode) ? DefaultMode : req.Mode.Trim();
        if (!mode.Equals("Test", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("Live", StringComparison.OrdinalIgnoreCase))
            return Fail("invalid_mode", "Mode must be 'Test' or 'Live'.");

        var chosen = (req.Methods ?? []).Select(m => m.Trim()).Where(m => m.Length > 0).Distinct().ToList();
        if (chosen.Count == 0) return Fail("methods_required", "Choose at least one payment method.");

        var unknown = chosen.Where(m => !gateways.Value!.Any(g => Same(g.Key, m))).ToList();
        if (unknown.Count > 0)
            return Fail("method_unknown", $"Unknown payment method: {string.Join(", ", unknown)}.");

        // Caught here rather than at a customer's checkout, where it would look like an outage:
        // a store priced in USD simply cannot use an AMD-only processor.
        var mismatched = chosen
            .Select(m => gateways.Value!.First(g => Same(g.Key, m)))
            .Where(g => !Supports(g, store.Currency))
            .Select(g => g.DisplayName)
            .ToList();

        if (mismatched.Count > 0)
            return Fail("currency_unsupported", $"{string.Join(", ", mismatched)} cannot take {store.Currency}.");

        foreach (var key in chosen)
        {
            var gateway = gateways.Value!.First(g => Same(g.Key, key));

            var upsert = await _payments.UpsertAccountAsync(id, new UpsertGatewayAccountRequest(
                gateway.Key,
                mode,
                $"{store.Name} - {gateway.DisplayName}",
                credentialRef,
                // Providers rotate the webhook secret separately from the API key, but both resolve
                // from one reference until an admin needs to split them.
                credentialRef,
                IsEnabled: true,
                Priority: chosen.IndexOf(key),
                SupportedCurrencies: [store.Currency]), HttpContext.RequestAborted);

            if (!upsert.IsSuccess)
                return upsert.Problem == PaymentClientProblem.MerchantNotConfigured
                    ? Fail(upsert.Code ?? "credentials_not_resolvable", upsert.Detail)
                    : Upstream(upsert.Code, upsert.Detail);
        }

        // Anything the store used to offer and no longer does.
        foreach (var account in accounts.Value!.Where(a => a.IsEnabled && !chosen.Any(k => Same(a.GatewayKey, k))))
        {
            var disabled = await _payments.DisableAccountAsync(id, account.Id, HttpContext.RequestAborted);
            if (!disabled.IsSuccess) return Upstream(disabled.Code, disabled.Detail);
        }

        // Last, and only once the gateways are actually in place.
        store.PaymentsEnabled = true;
        await _db.SaveChangesAsync();

        return Ok(Describe(store, gateways.Value!, await ReloadAsync(id)));
    }

    private async Task<IReadOnlyList<GatewayAccountDto>> ReloadAsync(Guid storeId)
    {
        var accounts = await _payments.ListAccountsAsync(storeId, HttpContext.RequestAborted);
        return accounts.IsSuccess ? accounts.Value! : [];
    }

    private static StorePaymentSettingsDto Describe(
        Store store, IReadOnlyList<PaymentGatewayDto> gateways, IReadOnlyList<GatewayAccountDto> accounts)
    {
        var enabled = accounts.Where(a => a.IsEnabled).ToList();

        var methods = gateways
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PaymentMethodDto(
                g.Key,
                g.DisplayName,
                g.SupportedCurrencies,
                Supports(g, store.Currency),
                enabled.Any(a => Same(a.GatewayKey, g.Key))))
            .ToList();

        // Every enrolment is written with the same reference and mode, so the first one describes
        // them all — the screen edits one setting, not one per method.
        var current = enabled.FirstOrDefault();

        return new StorePaymentSettingsDto(
            store.Id,
            store.Name,
            store.Currency,
            store.PaymentsEnabled,
            ServiceAvailable: true,
            ServiceProblem: null,
            current?.CredentialRef,
            current?.Mode ?? DefaultMode,
            methods);
    }

    private static StorePaymentSettingsDto Unavailable(Store store, string problem) => new(
        store.Id, store.Name, store.Currency, store.PaymentsEnabled,
        ServiceAvailable: false, ServiceProblem: problem,
        CredentialRef: null, Mode: DefaultMode, Methods: []);

    /// <summary>Empty means the gateway takes any currency; populated, it is a hard filter.</summary>
    private static bool Supports(PaymentGatewayDto gateway, string currency) =>
        gateway.SupportedCurrencies.Count == 0 ||
        gateway.SupportedCurrencies.Any(c => Same(c, currency));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private IActionResult Fail(string code, string? detail) =>
        BadRequest(new { code, error = detail ?? code });

    /// <summary>PaymentService itself is the problem, so the admin is told to retry rather than to fix input.</summary>
    private IActionResult Upstream(string? code, string? detail) =>
        StatusCode(StatusCodes.Status502BadGateway, new
        {
            code = code ?? "payment_service_unavailable",
            error = detail ?? "The payment service did not answer."
        });
}
