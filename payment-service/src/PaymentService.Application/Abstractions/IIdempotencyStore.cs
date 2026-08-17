namespace PaymentService.Application.Abstractions;

public enum IdempotencyOutcome
{
    /// <summary>First time this key has been seen. The caller should go ahead and do the work.</summary>
    Proceed = 0,

    /// <summary>Seen before and finished. Replay the stored response verbatim.</summary>
    Replay = 1,

    /// <summary>
    /// Seen before and still running. A 409 rather than a wait: the honest answer is "ask again",
    /// and blocking would just move the timeout that caused the retry.
    /// </summary>
    InProgress = 2,

    /// <summary>Same key, different body. The caller has a bug and should hear about it.</summary>
    Conflict = 3
}

public sealed record IdempotencyReservation(
    IdempotencyOutcome Outcome,
    int? ResponseStatusCode = null,
    string? ResponseBody = null);

/// <summary>
/// Makes retrying a payment request safe.
/// <para>
/// The reservation is written and committed <em>before</em> the work starts, which is the whole
/// point: a duplicate arriving while the first request is still in flight has to find something
/// already there, or two concurrent retries both charge the customer.
/// </para>
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Claims the key, or reports what is already known about it.</summary>
    Task<IdempotencyReservation> ReserveAsync(
        Guid merchantId, string key, string endpoint, string requestHash, CancellationToken ct = default);

    /// <summary>Stores the response so later duplicates can replay it.</summary>
    Task CompleteAsync(
        Guid merchantId, string key, int statusCode, string? responseBody, Guid? paymentId = null, CancellationToken ct = default);

    /// <summary>
    /// Drops the reservation when the work failed in a way worth retrying, so a genuine retry is
    /// not permanently answered with a 409 for a payment that never happened.
    /// </summary>
    Task ReleaseAsync(Guid merchantId, string key, CancellationToken ct = default);
}
