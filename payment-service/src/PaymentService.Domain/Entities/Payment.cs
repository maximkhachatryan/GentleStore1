using PaymentService.Domain.Common;
using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// One attempt to collect one amount for one order. Not a ledger of a customer's money and not a
/// wallet — if a customer pays twice for the same order, that is two payments, one of which gets
/// refunded.
/// <para>
/// Everything the merchant might want to read back is snapshotted here rather than fetched from
/// the gateway on demand: gateways expire their records, rate-limit reads, and go down at exactly
/// the moment someone is trying to work out whether a customer was charged.
/// </para>
/// </summary>
public class Payment
{
    public Guid Id { get; set; }

    /// <summary>
    /// GentleStore's <c>StoreId</c>. Opaque here on purpose — this service knows nothing about
    /// stores, slugs or catalogues, only that payments belong to tenants.
    /// </summary>
    public Guid MerchantId { get; set; }

    /// <summary>Short code both sides can quote in a support conversation ("PAY-7QK4M2XT").</summary>
    public string Reference { get; set; } = string.Empty;

    // ---- What is being paid for (snapshot from the caller) ----

    /// <summary>The caller's order id. Not a foreign key — the orders table lives in another service.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>The human order number ("BS-0042"), so support does not have to resolve a GUID.</summary>
    public string? OrderNumber { get; set; }

    public string? Description { get; set; }

    // ---- Routing ----

    /// <summary>Registry key of the gateway handling this payment ("stripe", "adyen").</summary>
    public string GatewayKey { get; set; } = string.Empty;

    public Guid GatewayAccountId { get; set; }

    /// <summary>The gateway's own id. Null until the gateway has accepted the payment.</summary>
    public string? GatewayPaymentId { get; set; }

    /// <summary>Last raw status string the gateway reported, kept for support and for mapping bugs.</summary>
    public string? GatewayStatusCode { get; set; }

    // ---- Money ----

    public string Currency { get; set; } = string.Empty;

    /// <summary>What was asked for. Never changes once the payment exists.</summary>
    public long AmountMinor { get; set; }

    public long AuthorizedMinor { get; set; }
    public long CapturedMinor { get; set; }
    public long RefundedMinor { get; set; }

    public CaptureMode CaptureMode { get; set; }
    public PaymentStatus Status { get; set; }

    // ---- Customer snapshot ----

    /// <summary>The caller's customer id, so a merchant can find every payment by one person.</summary>
    public Guid? CustomerId { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }

    // ---- Method summary ----
    // Never card numbers. The most that is ever stored is what a receipt may show: "Visa ****4242".

    public PaymentMethodKind MethodKind { get; set; }
    public string? MethodBrand { get; set; }
    public string? MethodLast4 { get; set; }

    // ---- Redirect flow ----

    /// <summary>Where the gateway sends the customer once they are done. Required by hosted flows.</summary>
    public string? ReturnUrl { get; set; }

    public string? CancelUrl { get; set; }

    /// <summary>
    /// Where this service posts status changes. Per payment rather than per merchant so a caller
    /// can route storefront and back-office traffic differently, and so the URL a payment was
    /// created with keeps working after merchant configuration changes.
    /// </summary>
    public string? CallbackUrl { get; set; }

    // ---- Failure ----

    /// <summary>Gateway's own code, unmapped. Two gateways' "insufficient funds" are not the same string.</summary>
    public string? FailureCode { get; set; }

    public string? FailureMessage { get; set; }

    // ---- Caller data ----

    /// <summary>Opaque key/value bag echoed back on reads and callbacks. Never interpreted here.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// The key the caller used to create this payment. Stored on the row as well as in the
    /// idempotency table so a duplicate create can be traced back to the payment it produced.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    // ---- Timestamps ----

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// When the gateway's window closes. The reconciler uses this to stop waiting rather than
    /// leaving abandoned checkouts <see cref="PaymentStatus.Pending"/> forever.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Guards against two webhooks for the same payment landing at once and one overwriting the
    /// other's amounts. EF Core turns a mismatch into a retryable concurrency exception.
    /// </summary>
    public uint Version { get; set; }

    public MerchantGatewayAccount? GatewayAccount { get; set; }
    public ICollection<PaymentAttempt> Attempts { get; set; } = new List<PaymentAttempt>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    // ---- Derived ----

    public Money Amount => Money.FromMinorUnits(AmountMinor, Currency);
    public Money Authorized => Money.FromMinorUnits(AuthorizedMinor, Currency);
    public Money Captured => Money.FromMinorUnits(CapturedMinor, Currency);
    public Money Refunded => Money.FromMinorUnits(RefundedMinor, Currency);

    /// <summary>How much of the authorization is still available to capture.</summary>
    public Money CapturableRemaining =>
        Money.FromMinorUnits(Math.Max(0, AuthorizedMinor - CapturedMinor), Currency);

    /// <summary>How much of what was actually taken can still be given back.</summary>
    public Money RefundableRemaining =>
        Money.FromMinorUnits(Math.Max(0, CapturedMinor - RefundedMinor), Currency);
}
