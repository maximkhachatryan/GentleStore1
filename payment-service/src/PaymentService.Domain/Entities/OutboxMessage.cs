using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// A status change owed to the caller, written in the same transaction as the change itself.
/// <para>
/// A payment that succeeded is worth nothing to GentleStore if the notification is lost, and
/// posting to the caller inline would mean either committing before the post (losing it when the
/// post fails) or holding a database transaction open across an HTTP call to a service that may be
/// down. The outbox makes the record and the promise to deliver it atomic; a background dispatcher
/// keeps the promise afterwards, with retries.
/// </para>
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }

    public Guid MerchantId { get; set; }
    public Guid PaymentId { get; set; }

    /// <summary>This service's own event name — "payment.captured", "refund.succeeded".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The serialized body to POST. Built at enqueue time so the delivery reflects the state that fired it.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Snapshotted from the payment: the destination must not move after the fact.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; }

    public int Attempts { get; set; }

    /// <summary>Backoff marker. The dispatcher only picks up rows whose time has come.</summary>
    public DateTime NextAttemptAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public string? LastError { get; set; }
}
