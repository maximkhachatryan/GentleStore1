using PaymentService.Domain.Enums;

namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>Exactly what arrived on the webhook endpoint, unmodified.</summary>
/// <param name="RawBody">
/// The body as received. Signatures are computed over the exact bytes a gateway sent, so
/// deserializing and re-serializing before verification silently breaks it — key order and
/// whitespace both matter.
/// </param>
public sealed record WebhookDelivery(
    string RawBody,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query);

/// <summary>
/// One candidate the delivery might belong to. Webhooks arrive unauthenticated and identify
/// themselves only through their signature, so the reader is handed every enabled account for that
/// gateway and reports which one's secret verified.
/// </summary>
public sealed record GatewayWebhookAccount(
    Guid AccountId,
    Guid MerchantId,
    GatewayMode Mode,
    GatewayCredentials Secrets);

/// <summary>A verified notification, mapped into this service's vocabulary.</summary>
/// <param name="EventId">
/// The gateway's event id, used to dedupe redeliveries. An integration whose gateway sends no
/// event id must derive a stable one from the payload — not a random value, which would defeat it.
/// </param>
public sealed record WebhookNotification(
    string EventId,
    string EventType,
    string? GatewayPaymentId = null,
    string? GatewayRefundId = null,
    GatewayPaymentSnapshot? Payment = null,
    GatewayRefundSnapshot? Refund = null,
    DateTime? OccurredAt = null);

public enum WebhookReadStatus
{
    Ok = 0,

    /// <summary>No configured account's secret verified it. Never processed, always recorded.</summary>
    InvalidSignature = 1,

    /// <summary>Signature was fine but the body could not be understood — usually an API version drift.</summary>
    Unparseable = 2,

    /// <summary>
    /// Understood and deliberately not interesting. Gateways send far more event types than a
    /// payment service needs, and dropping them here keeps the inbox meaningful.
    /// </summary>
    Ignored = 3
}

public sealed record WebhookReadResult(
    WebhookReadStatus Status,
    WebhookNotification? Notification = null,
    Guid? MatchedAccountId = null,
    string? Detail = null)
{
    public static WebhookReadResult Ok(WebhookNotification notification, Guid accountId) =>
        new(WebhookReadStatus.Ok, notification, accountId);

    public static WebhookReadResult InvalidSignature(string? detail = null) =>
        new(WebhookReadStatus.InvalidSignature, Detail: detail);

    public static WebhookReadResult Unparseable(string? detail = null) =>
        new(WebhookReadStatus.Unparseable, Detail: detail);

    public static WebhookReadResult Ignored(string? detail = null, Guid? accountId = null) =>
        new(WebhookReadStatus.Ignored, MatchedAccountId: accountId, Detail: detail);
}

/// <summary>
/// Implemented alongside <see cref="IPaymentGateway"/> by gateways that push notifications. Split
/// from it because a poll-only gateway should not have to supply a stub, and because verification
/// is the one part of an integration that is security-critical on its own.
/// </summary>
public interface IGatewayWebhookReader
{
    /// <summary>Must match the <see cref="IPaymentGateway.Key"/> of the gateway it belongs to.</summary>
    string Key { get; }

    /// <summary>
    /// Verifies and interprets a delivery. Implementations must verify the signature against
    /// <paramref name="candidates"/> before parsing anything, and must compare using a
    /// constant-time comparison — a timing-variable one leaks the expected signature byte by byte.
    /// </summary>
    Task<WebhookReadResult> ReadAsync(
        WebhookDelivery delivery,
        IReadOnlyList<GatewayWebhookAccount> candidates,
        CancellationToken ct = default);
}
