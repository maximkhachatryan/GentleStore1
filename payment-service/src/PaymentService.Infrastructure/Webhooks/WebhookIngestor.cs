using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Notifications;
using PaymentService.Application.Webhooks;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Notifications;
using PaymentService.Infrastructure.Payments;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Webhooks;

/// <summary>
/// Store first, apply second.
/// <para>
/// A gateway retries a webhook until it gets a 2xx, so the endpoint's job is to make the delivery
/// durable and answer. Doing the work inside the request means a slow database turns into
/// duplicate deliveries and a failed application throws away a payload the gateway may not send
/// again.
/// </para>
/// </summary>
public sealed class WebhookIngestor : IWebhookIngestor
{
    private const string UniqueViolation = "23505";
    private const int MaxProcessingAttempts = 10;

    private readonly PaymentsDbContext _db;
    private readonly IPaymentGatewayRegistry _registry;
    private readonly IGatewayCredentialProvider _credentials;
    private readonly IPaymentStateApplier _applier;
    private readonly IOutboxEnqueuer _outbox;
    private readonly IReferenceGenerator _references;
    private readonly IClock _clock;
    private readonly ILogger<WebhookIngestor> _log;

    public WebhookIngestor(
        PaymentsDbContext db,
        IPaymentGatewayRegistry registry,
        IGatewayCredentialProvider credentials,
        IPaymentStateApplier applier,
        IOutboxEnqueuer outbox,
        IReferenceGenerator references,
        IClock clock,
        ILogger<WebhookIngestor> log)
    {
        _db = db;
        _registry = registry;
        _credentials = credentials;
        _applier = applier;
        _outbox = outbox;
        _references = references;
        _clock = clock;
        _log = log;
    }

    public async Task<WebhookIngestResult> IngestAsync(
        string gatewayKey, WebhookDelivery delivery, CancellationToken ct = default)
    {
        if (!_registry.TryGetWebhookReader(gatewayKey, out var reader))
            return new WebhookIngestResult(WebhookIngestStatus.UnknownGateway, Detail: gatewayKey);

        var candidates = await LoadCandidatesAsync(reader.Key, ct);
        var read = await reader.ReadAsync(delivery, candidates, ct);
        var now = _clock.UtcNow;

        // Rejected and unreadable deliveries are still recorded. They cost almost nothing, and a
        // sudden run of failed signatures is the clearest sign the endpoint is being probed.
        if (read.Status is WebhookReadStatus.InvalidSignature or WebhookReadStatus.Unparseable)
        {
            var status = read.Status == WebhookReadStatus.InvalidSignature
                ? GatewayEventStatus.InvalidSignature
                : GatewayEventStatus.Failed;

            // No trustworthy event id here — the body could not be believed or could not be read —
            // so a content hash stands in, which also collapses a flood of identical retries.
            await StoreAsync(reader.Key, BodyHash(delivery.RawBody), "unverified", delivery, status, now, read.Detail, ct);

            return new WebhookIngestResult(
                read.Status == WebhookReadStatus.InvalidSignature
                    ? WebhookIngestStatus.InvalidSignature
                    : WebhookIngestStatus.Unparseable,
                Detail: read.Detail);
        }

        if (read.Status == WebhookReadStatus.Ignored || read.Notification is null)
            return new WebhookIngestResult(WebhookIngestStatus.Ignored, Detail: read.Detail);

        var notification = read.Notification;

        var (stored, isDuplicate) = await StoreAsync(
            reader.Key, notification.EventId, notification.EventType, delivery,
            GatewayEventStatus.Received, now, null, ct);

        // The gateway has sent this one before. Answering 200 is what stops it retrying.
        if (isDuplicate)
            return new WebhookIngestResult(WebhookIngestStatus.Duplicate, stored?.Id, stored?.PaymentId);

        return await ApplyAsync(stored!, notification, ct);
    }

    public async Task<int> RetryFailedAsync(int max = 50, CancellationToken ct = default)
    {
        var stuck = await _db.GatewayEvents
            .Where(e => e.Status == GatewayEventStatus.Failed && e.ProcessingAttempts < MaxProcessingAttempts)
            .OrderBy(e => e.ReceivedAt)
            .Take(Math.Clamp(max, 1, 200))
            .ToListAsync(ct);

        var recovered = 0;

        foreach (var stored in stuck)
        {
            if (!_registry.TryGetWebhookReader(stored.GatewayKey, out var reader)) continue;

            // Rebuilt case-insensitively: header names are, but the dictionary rehydrated from
            // jsonb is not, and a reader looking for "X-Paynet-Signature" would miss it.
            var headers = new Dictionary<string, string>(stored.Headers, StringComparer.OrdinalIgnoreCase);
            var delivery = new WebhookDelivery(stored.Payload, headers, new Dictionary<string, string>());
            var candidates = await LoadCandidatesAsync(reader.Key, ct);

            // Re-read from the stored bytes rather than trusting a parse from an earlier version of
            // the integration: a mapping fix is exactly why a stuck event becomes processable.
            var read = await reader.ReadAsync(delivery, candidates, ct);
            if (read.Status != WebhookReadStatus.Ok || read.Notification is null) continue;

            // Ignored counts too: the event is no longer stuck, it just turned out to be a no-op.
            var result = await ApplyAsync(stored, read.Notification, ct);
            if (result.Status is WebhookIngestStatus.Applied or WebhookIngestStatus.Ignored) recovered++;
        }

        return recovered;
    }

    // ---- Internals ----

    /// <summary>
    /// Every enabled account for the gateway, with its webhook secret resolved. A delivery arrives
    /// with no identity beyond its signature, so the reader has to be told who it might be from.
    /// </summary>
    private async Task<IReadOnlyList<GatewayWebhookAccount>> LoadCandidatesAsync(string gatewayKey, CancellationToken ct)
    {
        var accounts = await _db.MerchantGatewayAccounts
            .Where(a => a.GatewayKey == gatewayKey && a.IsEnabled)
            .ToListAsync(ct);

        var candidates = new List<GatewayWebhookAccount>(accounts.Count);

        foreach (var account in accounts)
        {
            var reference = account.WebhookSecretRef ?? account.CredentialRef;
            var secrets = await _credentials.GetAsync(gatewayKey, reference, ct);

            candidates.Add(new GatewayWebhookAccount(
                account.Id, account.MerchantId, account.Mode, secrets ?? GatewayCredentials.Empty));
        }

        return candidates;
    }

    private async Task<(GatewayEvent? Stored, bool IsDuplicate)> StoreAsync(
        string gatewayKey, string externalEventId, string eventType, WebhookDelivery delivery,
        GatewayEventStatus status, DateTime now, string? error, CancellationToken ct)
    {
        var stored = new GatewayEvent
        {
            Id = Guid.NewGuid(),
            GatewayKey = gatewayKey,
            ExternalEventId = externalEventId,
            EventType = eventType,
            Status = status,
            Payload = delivery.RawBody,
            Headers = new Dictionary<string, string>(delivery.Headers),
            ReceivedAt = now,
            LastError = error
        };

        _db.GatewayEvents.Add(stored);

        try
        {
            await _db.SaveChangesAsync(ct);
            return (stored, false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // The unique index on (GatewayKey, ExternalEventId) is the deduplication mechanism, not
            // just a constraint — two concurrent deliveries of one event race here and one loses.
            _db.Entry(stored).State = EntityState.Detached;

            var existing = await _db.GatewayEvents.FirstOrDefaultAsync(
                e => e.GatewayKey == gatewayKey && e.ExternalEventId == externalEventId, ct);

            return (existing, true);
        }
    }

    private async Task<WebhookIngestResult> ApplyAsync(
        GatewayEvent stored, WebhookNotification notification, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        stored.ProcessingAttempts++;

        var gatewayPaymentId = notification.GatewayPaymentId ?? notification.Payment?.GatewayPaymentId;

        var payment = gatewayPaymentId is null
            ? null
            : await _db.Payments
                .Include(p => p.Refunds)
                .FirstOrDefaultAsync(p => p.GatewayKey == stored.GatewayKey && p.GatewayPaymentId == gatewayPaymentId, ct);

        if (payment is null)
        {
            // Gateways routinely deliver before their own create call has returned to us, so this
            // is a race rather than an error. Left Failed so the retry sweep picks it up once the
            // payment row exists.
            stored.Status = GatewayEventStatus.Failed;
            stored.LastError = $"No payment matches gateway id '{gatewayPaymentId}'.";
            await _db.SaveChangesAsync(ct);

            return new WebhookIngestResult(WebhookIngestStatus.ProcessingFailed, stored.Id, Detail: stored.LastError);
        }

        stored.PaymentId = payment.Id;
        stored.MerchantId = payment.MerchantId;
        stored.GatewayPaymentId = gatewayPaymentId;

        try
        {
            var changed = false;

            if (notification.Payment is { } snapshot)
            {
                var change = _applier.ApplyPayment(payment, snapshot, now);
                changed |= change.StatusChanged;

                if (change.StatusChanged && _outbox.EventTypeFor(payment.Status) is { } eventType)
                    _outbox.EnqueuePayment(payment, eventType, now);
            }

            if (notification.Refund is { } refundSnapshot)
                changed |= ApplyRefund(payment, refundSnapshot, now);

            // Distinguishing the two makes the inbox worth reading: a run of Ignored rows is a
            // gateway redelivering, while a run of Processed rows is real movement.
            stored.Status = changed ? GatewayEventStatus.Processed : GatewayEventStatus.Ignored;
            stored.ProcessedAt = now;

            await _db.SaveChangesAsync(ct);

            return new WebhookIngestResult(
                changed ? WebhookIngestStatus.Applied : WebhookIngestStatus.Ignored, stored.Id, payment.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another delivery for the same payment won the race. Left Failed so the sweep replays
            // it against fresh state rather than overwriting what the winner wrote.
            _log.LogInformation("Concurrent update while applying event {EventId} to payment {PaymentId}.",
                stored.Id, payment.Id);

            return await MarkFailedAsync(stored, "Concurrent update; will retry.", ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to apply gateway event {EventId}.", stored.Id);
            return await MarkFailedAsync(stored, ex.Message, ct);
        }
    }

    /// <summary>Returns true when the delivery actually moved something.</summary>
    private bool ApplyRefund(Payment payment, GatewayRefundSnapshot snapshot, DateTime now)
    {
        var refund = payment.Refunds.FirstOrDefault(r => r.GatewayRefundId == snapshot.GatewayRefundId);
        var isNew = refund is null;

        if (refund is null)
        {
            // A refund we did not initiate — issued from the gateway's own dashboard, which is how
            // a lot of small merchants actually work. Recording it keeps our totals honest rather
            // than reporting a payment as fully collected after the money went back.
            refund = new Refund
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                Reference = _references.NewRefundReference(),
                Currency = snapshot.Amount?.Currency ?? payment.Currency,
                AmountMinor = snapshot.Amount?.MinorUnits ?? 0,
                Status = RefundStatus.Pending,
                Reason = "Initiated at the gateway",
                GatewayRefundId = snapshot.GatewayRefundId,
                CreatedAt = now,
                UpdatedAt = now
            };

            payment.Refunds.Add(refund);
            _db.Refunds.Add(refund);
        }

        var statusChanged = _applier.ApplyRefund(payment, refund, snapshot, now);

        if (statusChanged && refund.Status == RefundStatus.Succeeded)
            _outbox.EnqueueRefund(payment, refund, PaymentEventTypes.RefundSucceeded, now);
        else if (statusChanged && refund.Status == RefundStatus.Failed)
            _outbox.EnqueueRefund(payment, refund, PaymentEventTypes.RefundFailed, now);

        return isNew || statusChanged;
    }

    private async Task<WebhookIngestResult> MarkFailedAsync(GatewayEvent stored, string error, CancellationToken ct)
    {
        // The entry may be poisoned by the failure that got us here, so the status is written with
        // a targeted update rather than by saving the tracked graph again.
        await _db.GatewayEvents
            .Where(e => e.Id == stored.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, GatewayEventStatus.Failed)
                .SetProperty(e => e.LastError, error.Length > 2000 ? error[..2000] : error)
                .SetProperty(e => e.ProcessingAttempts, stored.ProcessingAttempts), ct);

        return new WebhookIngestResult(WebhookIngestStatus.ProcessingFailed, stored.Id, Detail: error);
    }

    private static string BodyHash(string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
}
