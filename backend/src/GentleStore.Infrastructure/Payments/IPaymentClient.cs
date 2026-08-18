namespace GentleStore.Infrastructure.Payments;

/// <summary>
/// GentleStore's whole view of PaymentService.
/// <para>
/// Narrow on purpose. Everything about gateways, idempotency, webhooks, refunds and reconciliation
/// stays on the other side of this interface; a shop only needs to start a payment, ask what
/// happened to it, and let an admin enrol a store.
/// </para>
/// </summary>
public interface IPaymentClient
{
    /// <summary>False when no base URL is configured, in which case payments cannot be enabled at all.</summary>
    bool IsConfigured { get; }

    // ---- Admin: enrolling a store ----

    /// <summary>Payment methods this deployment can offer, so the admin screen lists real options.</summary>
    Task<PaymentClientResult<IReadOnlyList<PaymentGatewayDto>>> ListGatewaysAsync(CancellationToken ct = default);

    Task<PaymentClientResult<IReadOnlyList<GatewayAccountDto>>> ListAccountsAsync(Guid storeId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the store's enrolment with one gateway. PaymentService checks the
    /// credential reference resolves before saving, so a typo fails here rather than at a
    /// customer's checkout.
    /// </summary>
    Task<PaymentClientResult<GatewayAccountDto>> UpsertAccountAsync(
        Guid storeId, UpsertGatewayAccountRequest request, CancellationToken ct = default);

    /// <summary>Stops an account taking new payments. Never a delete — old payments must stay refundable.</summary>
    Task<PaymentClientResult<GatewayAccountDto>> DisableAccountAsync(
        Guid storeId, Guid accountId, CancellationToken ct = default);

    // ---- Checkout ----

    /// <param name="idempotencyKey">
    /// Derived from the order, so a customer who double-taps "Pay" or reloads mid-redirect gets the
    /// payment they already started rather than a second charge.
    /// </param>
    Task<PaymentClientResult<InitiatedPaymentDto>> CreatePaymentAsync(
        CreatePaymentRequest request, string idempotencyKey, CancellationToken ct = default);

    Task<PaymentClientResult<PaymentDto>> GetPaymentAsync(Guid storeId, Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// Makes PaymentService re-read the payment from the gateway. What to call when a customer
    /// returns from a hosted checkout: the query string they come back with was written by their
    /// own browser, so it proves nothing.
    /// </summary>
    Task<PaymentClientResult<PaymentDto>> SyncPaymentAsync(Guid storeId, Guid paymentId, CancellationToken ct = default);
}
