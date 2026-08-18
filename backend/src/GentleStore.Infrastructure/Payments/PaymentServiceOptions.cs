namespace GentleStore.Infrastructure.Payments;

/// <summary>
/// How to reach PaymentService. It is an internal service with no public route in the shipped
/// deployment, so there is nothing here that a customer's browser ever sees.
/// </summary>
public class PaymentServiceOptions
{
    public const string SectionName = "PaymentService";

    /// <summary>
    /// Base URL of the payment service. Empty switches the integration off entirely: the admin
    /// screen says so, and no store can have payments enabled.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Sent as <c>X-Internal-Key</c>. Optional — PaymentService only checks it when it has keys configured.</summary>
    public string? InternalKey { get; set; }

    /// <summary>
    /// Shared secret PaymentService signs its status callbacks with. Unset means callbacks are
    /// accepted unsigned, which is only safe while both services are on a private network.
    /// </summary>
    public string? CallbackSecret { get; set; }

    /// <summary>
    /// Public origin of this API, used to tell PaymentService where to post callbacks. Falls back
    /// to the incoming request's own origin, which is right in development and usually right
    /// behind a proxy that sets X-Forwarded-Proto and Host.
    /// </summary>
    public string? CallbackBaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>True when a base URL is configured and the integration can be used at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
