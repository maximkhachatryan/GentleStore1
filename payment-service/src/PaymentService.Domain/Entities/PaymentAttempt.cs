using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// One call to a gateway, written before the call goes out and updated when it comes back.
/// <para>
/// Written first on purpose. If the process dies mid-call there is still a row saying "we asked
/// gateway X to charge Y and never heard back", which is the only way to later find out whether
/// the customer was charged. A log line is not enough — logs are sampled and expire; this is the
/// evidence a chargeback dispute is answered with.
/// </para>
/// </summary>
public class PaymentAttempt
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }

    /// <summary>Set when the attempt concerns one refund rather than the payment as a whole.</summary>
    public Guid? RefundId { get; set; }

    public PaymentOperation Operation { get; set; }
    public AttemptOutcome Outcome { get; set; }

    /// <summary>The gateway's id for whatever this call created or touched.</summary>
    public string? GatewayReference { get; set; }

    /// <summary>The idempotency key sent to the gateway, so a retry can reuse it verbatim.</summary>
    public string? GatewayIdempotencyKey { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The gateway's raw response, redacted by the integration before it gets here. Kept because
    /// mapping bugs are only diagnosable against what the gateway actually said, and truncated
    /// because some gateways return very large bodies.
    /// </summary>
    public string? ResponseSnapshot { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Round-trip time. Cheap to record and the first thing looked at when checkout slows down.</summary>
    public int? DurationMs { get; set; }

    public Payment? Payment { get; set; }
}
