namespace PaymentService.Application.Abstractions;

/// <summary>
/// Injected rather than called statically because payments are full of deadlines — authorization
/// windows, idempotency-key lifetimes, outbox backoff — and none of those are testable against a
/// clock that cannot be moved.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
