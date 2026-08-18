using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Infrastructure.Gateways.Paynet;

/// <summary>
/// Verifies and interprets Paynet's notifications.
/// <para>
/// Paynet signs the body with HMAC-SHA256 under the merchant's webhook secret and sends it as
/// <c>X-Paynet-Signature: sha256=&lt;hex&gt;</c>. The delivery is otherwise unauthenticated — the
/// endpoint is public — so this signature is the only thing separating a real payment
/// notification from anyone who can guess the URL.
/// </para>
/// </summary>
public sealed class PaynetWebhookReader : IGatewayWebhookReader
{
    private const string SignatureHeader = "X-Paynet-Signature";
    private const string SignaturePrefix = "sha256=";
    private const string SecretName = "WebhookSecret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _processor;
    private readonly ILogger<PaynetWebhookReader> _log;

    public PaynetWebhookReader(string processor, ILogger<PaynetWebhookReader> log)
    {
        _processor = processor.Trim().ToLowerInvariant();
        _log = log;
    }

    public string Key => PaynetMapping.GatewayKey(_processor);

    public Task<WebhookReadResult> ReadAsync(
        WebhookDelivery delivery,
        IReadOnlyList<GatewayWebhookAccount> candidates,
        CancellationToken ct = default)
    {
        var presented = Header(delivery, SignatureHeader);
        if (string.IsNullOrWhiteSpace(presented))
            return Task.FromResult(WebhookReadResult.InvalidSignature($"No {SignatureHeader} header."));

        var expected = Decode(presented);
        if (expected is null)
            return Task.FromResult(WebhookReadResult.InvalidSignature($"{SignatureHeader} is not '{SignaturePrefix}<hex>'."));

        var payload = Encoding.UTF8.GetBytes(delivery.RawBody);
        var configured = 0;
        Guid? matched = null;

        // Every candidate is checked, without breaking out on the first hit. Returning early would
        // make the response time depend on which account matched, which is a side channel; the
        // comparison itself is constant-time for the same reason.
        foreach (var candidate in candidates)
        {
            var secret = candidate.Secrets.Get(SecretName);
            if (string.IsNullOrWhiteSpace(secret)) continue;

            configured++;
            var computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);

            if (CryptographicOperations.FixedTimeEquals(computed, expected)) matched = candidate.AccountId;
        }

        if (matched is null)
        {
            // Worth separating: during setup this is almost always a missing secret rather than a
            // forged delivery, and the two need very different responses from whoever is on call.
            var detail = configured == 0
                ? $"No enabled {Key} account has a '{SecretName}' configured, so no delivery can be verified."
                : "No configured webhook secret matches the signature.";

            _log.LogWarning("Rejected a {Gateway} webhook: {Detail}", Key, detail);
            return Task.FromResult(WebhookReadResult.InvalidSignature(detail));
        }

        PaynetWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PaynetWebhookEnvelope>(delivery.RawBody, Json);
        }
        catch (JsonException ex)
        {
            return Task.FromResult(WebhookReadResult.Unparseable(ex.Message));
        }

        if (envelope?.Transaction is not { } transaction || string.IsNullOrWhiteSpace(transaction.Uuid))
            return Task.FromResult(WebhookReadResult.Unparseable("The delivery carries no transaction uuid."));

        var snapshot = PaynetMapping.ToSnapshot(transaction, out var recognised);

        if (!recognised)
            _log.LogWarning(
                "Paynet sent status '{Status}' for {Uuid}, which this integration does not know. Treated as in-flight.",
                transaction.Status, transaction.Uuid);

        var notification = new WebhookNotification(
            EventId(transaction),
            envelope.Event ?? $"payment.{transaction.Status}",
            transaction.Uuid,
            Payment: snapshot);

        return Task.FromResult(WebhookReadResult.Ok(notification, matched.Value));
    }

    /// <summary>
    /// Derived from the payload rather than taken from <c>X-Paynet-Delivery</c>.
    /// <para>
    /// A delivery id identifies the attempt; what has to be deduplicated is the <em>event</em>. If
    /// Paynet's retries carry fresh delivery ids, keying on them would let the same status change
    /// through several times. Payment uuid plus status is stable across any number of retries and
    /// still changes the moment the payment actually moves, which is exactly the boundary the
    /// inbox's unique index should sit on.
    /// </para>
    /// </summary>
    private static string EventId(PaynetWebhookTransaction transaction) =>
        $"{transaction.Uuid}:{transaction.Status?.Trim().ToLowerInvariant() ?? "unknown"}";

    /// <summary>Hex after the <c>sha256=</c> prefix, or null if it is not in that shape.</summary>
    private static byte[]? Decode(string header)
    {
        var value = header.Trim();

        if (value.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            value = value[SignaturePrefix.Length..];

        try
        {
            var bytes = Convert.FromHexString(value);
            return bytes.Length == SHA256.HashSizeInBytes ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Case-insensitive by hand. HTTP header names are case-insensitive, and a replayed delivery
    /// comes back through a dictionary rehydrated from JSON that no longer remembers that.
    /// </summary>
    private static string? Header(WebhookDelivery delivery, string name)
    {
        if (delivery.Headers.TryGetValue(name, out var direct)) return direct;

        foreach (var (key, value) in delivery.Headers)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return value;

        return null;
    }
}
