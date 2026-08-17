using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// A merchant's enrolment with one gateway — which gateway, in which mode, and where to find the
/// credentials.
/// <para>
/// The credentials themselves are deliberately not here. This table is read by ordinary queries,
/// dumped into backups and copied to staging; API secrets that can move money should not travel
/// with it. The row holds a <see cref="CredentialRef"/> that an <c>IGatewayCredentialProvider</c>
/// resolves against configuration or a secret manager at call time.
/// </para>
/// </summary>
public class MerchantGatewayAccount
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }

    /// <summary>Registry key of the gateway ("stripe"). Matched case-insensitively.</summary>
    public string GatewayKey { get; set; } = string.Empty;

    public GatewayMode Mode { get; set; }

    /// <summary>Shown in the merchant's settings — "Stripe (live)".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Opaque lookup handle for the credential provider, never a secret itself.</summary>
    public string CredentialRef { get; set; } = string.Empty;

    /// <summary>
    /// A disabled account still resolves for existing payments — refunding last month's order must
    /// keep working after a merchant stops accepting new payments through that gateway.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Lowest wins when a merchant has more than one account able to take the payment.</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Empty means "whatever the gateway supports". Populated, it is a hard filter — the cheapest
    /// way to stop a EUR-only acquirer being handed a USD order.
    /// </summary>
    public List<string> SupportedCurrencies { get; set; } = new();

    /// <summary>
    /// Shared secret used to verify this account's inbound webhooks, again by reference only.
    /// Separate from <see cref="CredentialRef"/> because gateways rotate the two independently.
    /// </summary>
    public string? WebhookSecretRef { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
