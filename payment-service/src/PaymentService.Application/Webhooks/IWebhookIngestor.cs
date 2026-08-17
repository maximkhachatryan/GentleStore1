using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Application.Webhooks;

public enum WebhookIngestStatus
{
    /// <summary>Stored and applied.</summary>
    Applied = 0,

    /// <summary>Stored; the notification did not change anything (redelivery, or stale ordering).</summary>
    Ignored = 1,

    /// <summary>Seen before. Answered 200 so the gateway stops retrying.</summary>
    Duplicate = 2,

    /// <summary>Rejected. Recorded, and answered 400 so the gateway stops retrying a body we will never accept.</summary>
    InvalidSignature = 3,

    /// <summary>Verified but not understood. Kept for replay once the mapping is fixed.</summary>
    Unparseable = 4,

    /// <summary>Nothing is registered under that key.</summary>
    UnknownGateway = 5,

    /// <summary>Stored but not applied. Answered 500 so the gateway retries.</summary>
    ProcessingFailed = 6
}

public sealed record WebhookIngestResult(
    WebhookIngestStatus Status,
    Guid? EventId = null,
    Guid? PaymentId = null,
    string? Detail = null);

/// <summary>
/// The inbound half of the gateway conversation: takes a raw delivery, stores it, verifies it,
/// and applies it to a payment.
/// <para>
/// Verification and interpretation belong to the gateway's <see cref="IGatewayWebhookReader"/>;
/// what happens afterwards — deduplication, the status rules, the outbound callback — belongs
/// here, so every gateway gets the same guarantees.
/// </para>
/// </summary>
public interface IWebhookIngestor
{
    Task<WebhookIngestResult> IngestAsync(string gatewayKey, WebhookDelivery delivery, CancellationToken ct = default);

    /// <summary>
    /// Re-runs stored events that failed to apply. Separate from ingestion because a failure to
    /// apply is usually a transient database problem, and the gateway should not have to keep
    /// redelivering for us to recover.
    /// </summary>
    Task<int> RetryFailedAsync(int max = 50, CancellationToken ct = default);
}
