namespace PaymentService.Domain.Enums;

/// <summary>
/// The distinction that matters is retryable versus not. A decline is a final answer about this
/// payment; a timeout says nothing at all about it, and retrying the two has opposite consequences.
/// </summary>
public enum AttemptOutcome
{
    /// <summary>Sent, no answer yet. An attempt left here is the dangerous case — see <c>Unknown</c>.</summary>
    Started = 0,

    Succeeded = 1,

    /// <summary>The issuer said no. Business outcome, not an error — do not retry as-is.</summary>
    Declined = 2,

    /// <summary>The gateway refused the request itself: bad amount, unsupported currency, bad credentials.</summary>
    Rejected = 3,

    /// <summary>Timeout, socket error, 5xx. Safe to retry with the same idempotency key.</summary>
    TransientFailure = 4,

    /// <summary>The gateway answered, and the answer will not change on a retry.</summary>
    PermanentFailure = 5,

    /// <summary>
    /// We may or may not have charged the customer — a timeout after the request was already
    /// accepted. Never retried blind; resolved by fetching the payment from the gateway.
    /// </summary>
    Unknown = 6
}
