using PaymentService.Domain.Entities;

namespace PaymentService.Application.Notifications;

public sealed record CallbackDeliveryResult(bool Delivered, int? StatusCode = null, string? Error = null, bool Retryable = true);

/// <summary>
/// Delivers one outbox row to the caller that owns it. Kept behind an interface so the transport
/// can change — today an HTTP POST back to GentleStore, plausibly a queue later — without touching
/// the outbox or the orchestrator.
/// </summary>
public interface IMerchantCallbackSender
{
    Task<CallbackDeliveryResult> SendAsync(OutboxMessage message, CancellationToken ct = default);
}

/// <summary>
/// The event names this service emits. Deliberately fewer than any gateway's: these describe what
/// happened to the money, which is all a shop needs to decide whether to fulfil an order.
/// </summary>
public static class PaymentEventTypes
{
    /// <summary>Registered with a gateway; the customer may still have work to do.</summary>
    public const string Created = "payment.created";

    public const string RequiresAction = "payment.requires_action";

    /// <summary>Funds held, not taken. Only ever seen in a manual-capture flow.</summary>
    public const string Authorized = "payment.authorized";

    /// <summary>Money moved. This is the one that should release an order.</summary>
    public const string Captured = "payment.captured";

    public const string Failed = "payment.failed";
    public const string Cancelled = "payment.cancelled";
    public const string Expired = "payment.expired";

    public const string RefundCreated = "refund.created";
    public const string RefundSucceeded = "refund.succeeded";
    public const string RefundFailed = "refund.failed";
}
