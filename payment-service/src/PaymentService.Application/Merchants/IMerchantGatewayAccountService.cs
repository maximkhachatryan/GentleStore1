using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Merchants;

/// <param name="CredentialRef">
/// A handle the credential provider understands, never a secret. Sending an API key to this
/// endpoint would put it in request logs, which is exactly what the reference indirection avoids.
/// </param>
public sealed record UpsertGatewayAccountCommand(
    string GatewayKey,
    GatewayMode Mode,
    string DisplayName,
    string CredentialRef,
    string? WebhookSecretRef = null,
    bool IsEnabled = true,
    int Priority = 0,
    IReadOnlyList<string>? SupportedCurrencies = null);

public enum GatewayAccountProblem
{
    None = 0,
    UnknownGateway,
    InvalidCurrency,

    /// <summary>The credential reference resolves to nothing, so the account would never work.</summary>
    CredentialsNotResolvable,

    NotFound
}

public sealed record GatewayAccountResult(
    GatewayAccountProblem Problem,
    MerchantGatewayAccount? Account = null,
    string? Detail = null)
{
    public bool IsSuccess => Problem == GatewayAccountProblem.None && Account is not null;
}

/// <summary>
/// Enrolment management: which gateways a merchant is set up with. Small enough to live in the
/// orchestrator, kept separate because it is administrative — it never moves money, and it wants
/// different access control once there is any.
/// </summary>
public interface IMerchantGatewayAccountService
{
    Task<IReadOnlyList<MerchantGatewayAccount>> ListAsync(Guid merchantId, CancellationToken ct = default);

    Task<MerchantGatewayAccount?> GetAsync(Guid merchantId, Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the account for this (merchant, gateway, mode). An upsert because that
    /// triple is the natural key — a merchant has one Stripe live account, not a list of them.
    /// Validates the credential reference resolves, so a broken setup fails here rather than at
    /// the first checkout.
    /// </summary>
    Task<GatewayAccountResult> UpsertAsync(Guid merchantId, UpsertGatewayAccountCommand command, CancellationToken ct = default);

    /// <summary>
    /// Disables rather than deletes. Existing payments must still resolve their account to be
    /// refundable, and a merchant who turns a gateway off has not agreed to lose the ability to
    /// refund what it already took.
    /// </summary>
    Task<GatewayAccountResult> DisableAsync(Guid merchantId, Guid accountId, CancellationToken ct = default);
}
