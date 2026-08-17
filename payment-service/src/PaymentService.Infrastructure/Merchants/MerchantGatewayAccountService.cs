using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Merchants;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Merchants;

public sealed class MerchantGatewayAccountService : IMerchantGatewayAccountService
{
    private readonly PaymentsDbContext _db;
    private readonly IPaymentGatewayRegistry _registry;
    private readonly IGatewayCredentialProvider _credentials;
    private readonly IClock _clock;

    public MerchantGatewayAccountService(
        PaymentsDbContext db,
        IPaymentGatewayRegistry registry,
        IGatewayCredentialProvider credentials,
        IClock clock)
    {
        _db = db;
        _registry = registry;
        _credentials = credentials;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MerchantGatewayAccount>> ListAsync(Guid merchantId, CancellationToken ct = default) =>
        await _db.MerchantGatewayAccounts
            .Where(a => a.MerchantId == merchantId)
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.GatewayKey)
            .ToListAsync(ct);

    public Task<MerchantGatewayAccount?> GetAsync(Guid merchantId, Guid accountId, CancellationToken ct = default) =>
        _db.MerchantGatewayAccounts.FirstOrDefaultAsync(a => a.MerchantId == merchantId && a.Id == accountId, ct);

    public async Task<GatewayAccountResult> UpsertAsync(
        Guid merchantId, UpsertGatewayAccountCommand command, CancellationToken ct = default)
    {
        if (!_registry.TryGet(command.GatewayKey, out var gateway))
            return new GatewayAccountResult(
                GatewayAccountProblem.UnknownGateway,
                Detail: $"No gateway is registered under '{command.GatewayKey}'.");

        var currencies = new List<string>();
        foreach (var currency in command.SupportedCurrencies ?? [])
        {
            if (!Currencies.IsWellFormed(currency))
                return new GatewayAccountResult(
                    GatewayAccountProblem.InvalidCurrency, Detail: $"'{currency}' is not a three-letter currency code.");

            var normalized = Currencies.Normalize(currency);

            // A merchant listing a currency the integration cannot handle would produce an account
            // that routes and then fails at the gateway — better to refuse it here.
            if (gateway.SupportedCurrencies.Count > 0 &&
                !gateway.SupportedCurrencies.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                return new GatewayAccountResult(
                    GatewayAccountProblem.InvalidCurrency,
                    Detail: $"{gateway.DisplayName} does not support {normalized}.");

            if (!currencies.Contains(normalized)) currencies.Add(normalized);
        }

        // Checked now rather than at the first checkout. A merchant finding out their credentials
        // are wrong when a customer is trying to pay is the expensive version of this error.
        var resolved = await _credentials.GetAsync(gateway.Key, command.CredentialRef, ct);
        if (resolved is null)
            return new GatewayAccountResult(
                GatewayAccountProblem.CredentialsNotResolvable,
                Detail: $"Credentials '{command.CredentialRef}' for '{gateway.Key}' could not be resolved.");

        var now = _clock.UtcNow;

        var account = await _db.MerchantGatewayAccounts.FirstOrDefaultAsync(
            a => a.MerchantId == merchantId && a.GatewayKey == gateway.Key && a.Mode == command.Mode, ct);

        if (account is null)
        {
            account = new MerchantGatewayAccount
            {
                Id = Guid.NewGuid(),
                MerchantId = merchantId,
                GatewayKey = gateway.Key,
                Mode = command.Mode,
                CreatedAt = now
            };

            _db.MerchantGatewayAccounts.Add(account);
        }

        account.DisplayName = command.DisplayName;
        account.CredentialRef = command.CredentialRef;
        account.WebhookSecretRef = command.WebhookSecretRef;
        account.IsEnabled = command.IsEnabled;
        account.Priority = command.Priority;
        account.SupportedCurrencies = currencies;
        account.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return new GatewayAccountResult(GatewayAccountProblem.None, account);
    }

    public async Task<GatewayAccountResult> DisableAsync(
        Guid merchantId, Guid accountId, CancellationToken ct = default)
    {
        var account = await GetAsync(merchantId, accountId, ct);
        if (account is null) return new GatewayAccountResult(GatewayAccountProblem.NotFound);

        account.IsEnabled = false;
        account.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new GatewayAccountResult(GatewayAccountProblem.None, account);
    }
}
