using System.Text.Json;
using PaymentService.Application.Notifications;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Notifications;

/// <summary>
/// Queues a status change for delivery. Adds to the change tracker without saving, so the message
/// commits in the same transaction as the state it describes — that atomicity is the entire point
/// of the outbox.
/// </summary>
public interface IOutboxEnqueuer
{
    void EnqueuePayment(Payment payment, string eventType, DateTime now);
    void EnqueueRefund(Payment payment, Refund refund, string eventType, DateTime now);

    /// <summary>The event a payment's current status should announce, or null when it is not worth announcing.</summary>
    string? EventTypeFor(PaymentStatus status);
}

public sealed class OutboxEnqueuer : IOutboxEnqueuer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PaymentsDbContext _db;

    public OutboxEnqueuer(PaymentsDbContext db) => _db = db;

    public string? EventTypeFor(PaymentStatus status) => status switch
    {
        PaymentStatus.RequiresAction => PaymentEventTypes.RequiresAction,
        PaymentStatus.Authorized => PaymentEventTypes.Authorized,
        PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded => PaymentEventTypes.Captured,
        PaymentStatus.Failed => PaymentEventTypes.Failed,
        PaymentStatus.Cancelled => PaymentEventTypes.Cancelled,
        PaymentStatus.Expired => PaymentEventTypes.Expired,

        // Pending and Processing say nothing a caller can act on; announcing them would be noise
        // the caller has to filter out.
        _ => null
    };

    public void EnqueuePayment(Payment payment, string eventType, DateTime now) =>
        Add(payment, eventType, now, Payload(payment, eventType, null, now));

    public void EnqueueRefund(Payment payment, Refund refund, string eventType, DateTime now) =>
        Add(payment, eventType, now, Payload(payment, eventType, refund, now));

    private void Add(Payment payment, string eventType, DateTime now, PaymentCallbackPayload payload)
    {
        // No callback URL means the caller polls. Nothing to queue, and queueing anyway would fill
        // the table with rows that can never be delivered.
        if (string.IsNullOrWhiteSpace(payment.CallbackUrl)) return;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MerchantId = payment.MerchantId,
            PaymentId = payment.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, Json),
            CallbackUrl = payment.CallbackUrl,
            Status = OutboxStatus.Pending,
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    private static PaymentCallbackPayload Payload(Payment payment, string eventType, Refund? refund, DateTime now) => new(
        eventType,
        payment.Id,
        payment.Reference,
        payment.MerchantId,
        payment.OrderId,
        payment.OrderNumber,
        payment.Status.ToString(),
        payment.Currency,
        payment.Amount.ToDecimal(),
        payment.Captured.ToDecimal(),
        payment.Refunded.ToDecimal(),
        payment.GatewayKey,
        payment.FailureCode,
        payment.FailureMessage,
        refund?.Id,
        refund?.Reference,
        refund?.Amount.ToDecimal(),
        refund?.Status.ToString(),
        payment.Metadata,
        now);
}
