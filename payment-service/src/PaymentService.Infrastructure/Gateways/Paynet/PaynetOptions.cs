namespace PaymentService.Infrastructure.Gateways.Paynet;

/// <summary>
/// Deployment-wide Paynet settings. Per-merchant values (API key, webhook secret, verified domain)
/// are not here — they come from <c>IGatewayCredentialProvider</c> at call time.
/// </summary>
public sealed class PaynetOptions
{
    public const string SectionName = "Gateways:paynet";

    public bool Enabled { get; set; }

    /// <summary>
    /// One base URL for both environments. Paynet decides test versus live from the API key, not
    /// the host, so a merchant switches by swapping the key — there is no sandbox URL to point at.
    /// </summary>
    public string BaseUrl { get; set; } = "https://paynet.am/api";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Processor slugs to expose, one registered gateway each: <c>paynet-arca</c>,
    /// <c>paynet-idram</c>, <c>paynet-telcell</c>.
    /// <para>
    /// Separate gateways rather than one with a setting, because to a customer these are different
    /// payment methods — "card", "Idram", "Telcell" — and a shop offers several at once. Making the
    /// choice a gateway key means the storefront picks one the same way it would pick between two
    /// different providers, and the merchant enrols each with the same API key.
    /// </para>
    /// </summary>
    public List<string> Processors { get; set; } = new() { "arca", "idram", "telcell" };
}
