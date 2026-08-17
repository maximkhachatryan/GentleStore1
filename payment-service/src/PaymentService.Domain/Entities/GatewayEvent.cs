using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// The webhook inbox: every inbound notification is written here first and applied second.
/// <para>
/// Store-then-apply because a gateway retries until it gets a 2xx, and applying inside the request
/// means a slow database turns into duplicate deliveries and a fast one still loses the payload
/// when application fails. With the row on disk the endpoint can answer immediately and processing
/// can be retried against the original bytes.
/// </para>
/// <para>
/// Rows that fail signature verification are stored too. They cost almost nothing and a sudden run
/// of them is the clearest signal that someone is probing the endpoint.
/// </para>
/// </summary>
public class GatewayEvent
{
    public Guid Id { get; set; }

    public string GatewayKey { get; set; } = string.Empty;

    /// <summary>
    /// The gateway's own event id. Unique per gateway, which is what makes redelivery safe — the
    /// second copy collides on the index and is ignored instead of applying twice.
    /// </summary>
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>The gateway's event name, unmapped ("payment_intent.succeeded").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Null when the notification arrived before the payment could be identified.</summary>
    public Guid? PaymentId { get; set; }

    public Guid? MerchantId { get; set; }

    /// <summary>The gateway's payment id from the payload, kept even when it matches nothing here.</summary>
    public string? GatewayPaymentId { get; set; }

    public GatewayEventStatus Status { get; set; }

    /// <summary>The raw body exactly as received. Signatures are computed over bytes, not over a re-serialization.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Headers the gateway signs with, kept so verification can be replayed offline.</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public int ProcessingAttempts { get; set; }
    public string? LastError { get; set; }
}
