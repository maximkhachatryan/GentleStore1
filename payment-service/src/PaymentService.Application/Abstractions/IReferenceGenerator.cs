namespace PaymentService.Application.Abstractions;

/// <summary>
/// Produces the short codes people quote at each other — "PAY-7QK4M2XT", "REF-3B8HN0PZ".
/// <para>
/// Random rather than sequential: a payment reference travels in redirect URLs and receipts, and a
/// guessable one would let anyone enumerate a merchant's payments. GentleStore's order numbers are
/// sequential because they are per-store and never leave the store's own screens; these are not.
/// </para>
/// </summary>
public interface IReferenceGenerator
{
    string NewPaymentReference();
    string NewRefundReference();

    /// <summary>
    /// The key sent to the gateway for one logical operation. Derived from ids we already hold, so
    /// a retry of the same operation regenerates the same key and the gateway collapses it.
    /// </summary>
    string GatewayIdempotencyKey(Guid entityId, string operation);
}
