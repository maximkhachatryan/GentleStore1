namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>
/// The port every payment gateway integration implements. Adding Stripe, Adyen or a local acquirer
/// means one class here and one configuration section — nothing in the orchestrator, the API or
/// GentleStore changes.
/// <para>
/// Implementations must be stateless and thread-safe: one instance is registered as a singleton
/// and serves every merchant, with the per-merchant credentials arriving on
/// <see cref="GatewayContext"/> for each call.
/// </para>
/// <para>
/// Implementations must not throw for a gateway that declines or is unreachable — those are
/// results, returned through <see cref="GatewayResult{T}"/> so the orchestrator can record an
/// attempt and decide what it means. An exception is reserved for a bug in the integration itself.
/// </para>
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Stable lowercase identifier used in configuration, routes and the <c>GatewayKey</c> column.</summary>
    string Key { get; }

    /// <summary>Shown to merchants when they pick a provider.</summary>
    string DisplayName { get; }

    GatewayCapabilities Capabilities { get; }

    /// <summary>
    /// Currency codes this integration can handle at all. Empty means "no restriction" — what a
    /// given merchant's account is actually allowed to take is narrowed further by
    /// <c>MerchantGatewayAccount.SupportedCurrencies</c>.
    /// </summary>
    IReadOnlyCollection<string> SupportedCurrencies { get; }

    /// <summary>
    /// Registers the payment with the gateway. Returns the gateway's id plus, for hosted and
    /// client-side flows, the <see cref="CustomerAction"/> that tells the storefront where to send
    /// the customer next.
    /// </summary>
    Task<GatewayResult<GatewayPaymentSnapshot>> CreatePaymentAsync(
        GatewayCreatePaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Takes funds previously authorized. Only called when the gateway declares
    /// <see cref="GatewayCapabilities.ManualCapture"/>; a partial amount is only passed when it
    /// also declares <see cref="GatewayCapabilities.PartialCapture"/>.
    /// </summary>
    Task<GatewayResult<GatewayPaymentSnapshot>> CapturePaymentAsync(
        GatewayCaptureRequest request, CancellationToken ct = default);

    /// <summary>Releases an authorization, or abandons a payment the customer never completed.</summary>
    Task<GatewayResult<GatewayPaymentSnapshot>> CancelPaymentAsync(
        GatewayCancelRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reads the gateway's current view. This is the authority when a webhook was missed or an
    /// earlier call ended <see cref="Domain.Enums.AttemptOutcome.Unknown"/>, so it must be safe to
    /// call at any time and must never mutate anything.
    /// </summary>
    Task<GatewayResult<GatewayPaymentSnapshot>> FetchPaymentAsync(
        GatewayFetchRequest request, CancellationToken ct = default);

    /// <summary>Returns money. Called once per <c>Refund</c> row, with that row's own idempotency key.</summary>
    Task<GatewayResult<GatewayRefundSnapshot>> RefundAsync(
        GatewayRefundRequest request, CancellationToken ct = default);
}
