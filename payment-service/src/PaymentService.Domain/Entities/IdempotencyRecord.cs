namespace PaymentService.Domain.Entities;

/// <summary>
/// The answer already given for an <c>Idempotency-Key</c>, so a repeat of the same request replays
/// it instead of charging the customer a second time.
/// <para>
/// A checkout POST that times out gets retried — by the caller, by a proxy, or by a customer
/// pressing the button again. Without this, each retry is a fresh charge.
/// </para>
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; }

    /// <summary>Scoped to the merchant so two tenants cannot collide on a guessable key.</summary>
    public Guid MerchantId { get; set; }

    public string Key { get; set; } = string.Empty;

    /// <summary>Method and path. The same key against a different endpoint is a caller bug, not a retry.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the request body. A key reused with a different body means the caller is confused,
    /// and answering with the old response would hide a real mistake — so it is a 409 instead.
    /// </summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>Null while the first request is still in flight — that is what makes a concurrent duplicate a 409.</summary>
    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    /// <summary>Set once the request produced a payment, so a replay can be traced to its effect.</summary>
    public Guid? PaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Retries happen within minutes; keys are kept far longer and swept, not kept forever.</summary>
    public DateTime ExpiresAt { get; set; }
}
