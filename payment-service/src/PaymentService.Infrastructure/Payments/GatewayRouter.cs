using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Payments;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Payments;

public sealed record RoutedGateway(IPaymentGateway Gateway, MerchantGatewayAccount Account, GatewayContext Context);

/// <summary>
/// Decides which of a merchant's gateway accounts handles a payment, and assembles the context —
/// including the resolved credentials — that the integration is called with.
/// </summary>
public interface IGatewayRouter
{
    /// <summary>
    /// Picks an account for a payment that does not exist yet. <paramref name="preferredKey"/>
    /// narrows it to one gateway; otherwise the enabled accounts are tried in
    /// <c>Priority</c> order.
    /// </summary>
    Task<PaymentResult<RoutedGateway>> RouteNewAsync(
        Guid merchantId, string currency, string? preferredKey, CancellationToken ct = default);

    /// <summary>
    /// Re-resolves the account a payment was created with. Never re-routes: a payment can only be
    /// captured or refunded through the gateway that took it, so a disabled account still resolves
    /// here.
    /// </summary>
    Task<PaymentResult<RoutedGateway>> RouteExistingAsync(Payment payment, CancellationToken ct = default);
}

public sealed class GatewayRouter : IGatewayRouter
{
    private readonly PaymentsDbContext _db;
    private readonly IPaymentGatewayRegistry _registry;
    private readonly IGatewayCredentialProvider _credentials;

    public GatewayRouter(
        PaymentsDbContext db,
        IPaymentGatewayRegistry registry,
        IGatewayCredentialProvider credentials)
    {
        _db = db;
        _registry = registry;
        _credentials = credentials;
    }

    public async Task<PaymentResult<RoutedGateway>> RouteNewAsync(
        Guid merchantId, string currency, string? preferredKey, CancellationToken ct = default)
    {
        var accounts = await _db.MerchantGatewayAccounts
            .Where(a => a.MerchantId == merchantId && a.IsEnabled)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(ct);

        if (accounts.Count == 0)
            return PaymentResult<RoutedGateway>.Fail(
                PaymentProblem.NoGatewayConfigured,
                "This merchant has no enabled gateway account.");

        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            if (!_registry.TryGet(preferredKey, out _))
                return PaymentResult<RoutedGateway>.Fail(
                    PaymentProblem.UnknownGateway, $"No gateway is registered under '{preferredKey}'.");

            var named = accounts.Where(a => string.Equals(a.GatewayKey, preferredKey, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 0)
                return PaymentResult<RoutedGateway>.Fail(
                    PaymentProblem.GatewayDisabled,
                    $"This merchant has no enabled '{preferredKey}' account.");

            accounts = named;
        }

        // A gateway that is registered but has no account, or an account whose gateway is no
        // longer registered, are both survivable — skip and try the next rather than failing the
        // whole payment for a configuration gap the merchant may not even use.
        var eligible = accounts
            .Where(a => _registry.TryGet(a.GatewayKey, out _))
            .Where(a => AcceptsCurrency(a, currency))
            .ToList();

        if (eligible.Count == 0)
            return PaymentResult<RoutedGateway>.Fail(
                PaymentProblem.UnsupportedCurrency,
                $"No configured gateway account accepts {currency.ToUpperInvariant()}.");

        // First account whose credentials actually resolve. Trying rather than taking the first
        // blindly means one merchant's broken credential reference does not take out a gateway
        // they also have a working account for.
        string? lastDetail = null;
        foreach (var account in eligible)
        {
            var routed = await BuildAsync(account, ct);
            if (routed.IsSuccess) return routed;
            lastDetail = routed.Detail;
        }

        return PaymentResult<RoutedGateway>.Fail(PaymentProblem.GatewayMisconfigured, lastDetail);
    }

    public async Task<PaymentResult<RoutedGateway>> RouteExistingAsync(Payment payment, CancellationToken ct = default)
    {
        var account = payment.GatewayAccount
            ?? await _db.MerchantGatewayAccounts.FirstOrDefaultAsync(a => a.Id == payment.GatewayAccountId, ct);

        if (account is null)
            return PaymentResult<RoutedGateway>.Fail(
                PaymentProblem.GatewayMisconfigured,
                $"Gateway account {payment.GatewayAccountId} no longer exists.");

        return await BuildAsync(account, ct);
    }

    private async Task<PaymentResult<RoutedGateway>> BuildAsync(MerchantGatewayAccount account, CancellationToken ct)
    {
        if (!_registry.TryGet(account.GatewayKey, out var gateway))
            return PaymentResult<RoutedGateway>.Fail(
                PaymentProblem.UnknownGateway,
                $"Gateway '{account.GatewayKey}' is not registered in this build.");

        var credentials = await _credentials.GetAsync(account.GatewayKey, account.CredentialRef, ct);
        if (credentials is null)
            return PaymentResult<RoutedGateway>.Fail(
                PaymentProblem.GatewayMisconfigured,
                $"Credentials '{account.CredentialRef}' for gateway '{account.GatewayKey}' could not be resolved.");

        var context = new GatewayContext(
            account.MerchantId, account.Id, gateway.Key, account.Mode, credentials);

        return PaymentResult<RoutedGateway>.Ok(new RoutedGateway(gateway, account, context));
    }

    private static bool AcceptsCurrency(MerchantGatewayAccount account, string currency) =>
        account.SupportedCurrencies.Count == 0 ||
        account.SupportedCurrencies.Any(c => string.Equals(c, currency, StringComparison.OrdinalIgnoreCase));
}
