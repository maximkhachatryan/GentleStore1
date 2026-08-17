namespace PaymentService.Application.Notifications;

/// <summary>
/// The body POSTed to a caller's <c>CallbackUrl</c>.
/// <para>
/// Deliberately a summary and not the full payment: a callback is a signal to go and read, and
/// keeping the payload small means the contract barely changes as the payment model grows. It is
/// also unauthenticated from the receiver's point of view — anyone who learns the URL can post to
/// it — so the receiver is expected to treat this as a nudge and confirm against
/// <c>GET /api/payments/{id}</c> before releasing anything.
/// </para>
/// </summary>
public sealed record PaymentCallbackPayload(
    string EventType,
    Guid PaymentId,
    string Reference,
    Guid MerchantId,
    Guid? OrderId,
    string? OrderNumber,
    string Status,
    string Currency,
    decimal Amount,
    decimal CapturedAmount,
    decimal RefundedAmount,
    string GatewayKey,
    string? FailureCode,
    string? FailureMessage,
    Guid? RefundId,
    string? RefundReference,
    decimal? RefundAmount,
    string? RefundStatus,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime OccurredAt);
