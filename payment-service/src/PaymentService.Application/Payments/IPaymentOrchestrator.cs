using PaymentService.Domain.Entities;

namespace PaymentService.Application.Payments;

/// <summary>
/// The whole of what this service does, in one interface — everything the API exposes is a thin
/// wrapper over one of these.
/// <para>
/// It owns the parts that must not be duplicated per gateway: choosing an account, validating
/// against the gateway's declared capabilities, writing an attempt before and after every call,
/// applying a snapshot under the status rules, and queueing the callback in the same transaction.
/// An integration only has to speak its gateway's protocol.
/// </para>
/// </summary>
public interface IPaymentOrchestrator
{
    /// <summary>
    /// Creates the payment and registers it with a gateway. Safe to call twice with the same
    /// <c>IdempotencyKey</c> — the second call returns the first payment rather than making another.
    /// </summary>
    Task<PaymentResult<InitiatedPayment>> InitiateAsync(InitiatePaymentCommand command, CancellationToken ct = default);

    /// <summary>Takes funds held by an earlier authorization. Manual-capture flows only.</summary>
    Task<PaymentResult<Payment>> CaptureAsync(Guid merchantId, Guid paymentId, CapturePaymentCommand command, CancellationToken ct = default);

    /// <summary>Releases an authorization, or abandons a payment the customer walked away from.</summary>
    Task<PaymentResult<Payment>> CancelAsync(Guid merchantId, Guid paymentId, CancelPaymentCommand command, CancellationToken ct = default);

    /// <summary>Returns money. Each call creates its own <see cref="Refund"/>, so partials are first-class.</summary>
    Task<PaymentResult<Refund>> RefundAsync(Guid merchantId, Guid paymentId, RefundPaymentCommand command, CancellationToken ct = default);

    /// <summary>
    /// Re-reads the payment from the gateway and applies what comes back. The escape hatch for a
    /// missed webhook, and what the storefront should call when a customer returns from a redirect
    /// rather than trusting the query string it was sent back with.
    /// </summary>
    Task<PaymentResult<Payment>> SyncAsync(Guid merchantId, Guid paymentId, CancellationToken ct = default);

    Task<Payment?> GetAsync(Guid merchantId, Guid paymentId, CancellationToken ct = default);

    /// <summary>Lookup by the short code, which is what a customer or a receipt will have.</summary>
    Task<Payment?> GetByReferenceAsync(Guid merchantId, string reference, CancellationToken ct = default);

    Task<PagedResult<Payment>> ListAsync(PaymentQuery query, CancellationToken ct = default);
}
