using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Notifications;
using PaymentService.Application.Payments;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Notifications;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Everything that must happen the same way for every gateway.
/// <para>
/// The shape of each operation is deliberately identical: validate against what the gateway says
/// it can do, write an attempt and commit it, call the gateway, record what came back, fold the
/// result into the payment under the status rules, queue the callback in the same transaction.
/// Committing the attempt <em>before</em> the call is the part that matters — it is what leaves
/// evidence when the process dies mid-charge.
/// </para>
/// </summary>
public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    private const int MaxMetadataEntries = 20;
    private const int MaxMetadataKeyLength = 64;
    private const int MaxMetadataValueLength = 500;
    private const int MaxResponseSnapshot = 8000;
    private const int MaxPageSize = 200;

    private readonly PaymentsDbContext _db;
    private readonly IGatewayRouter _router;
    private readonly IPaymentStateApplier _applier;
    private readonly IOutboxEnqueuer _outbox;
    private readonly IReferenceGenerator _references;
    private readonly IClock _clock;
    private readonly ILogger<PaymentOrchestrator> _log;

    public PaymentOrchestrator(
        PaymentsDbContext db,
        IGatewayRouter router,
        IPaymentStateApplier applier,
        IOutboxEnqueuer outbox,
        IReferenceGenerator references,
        IClock clock,
        ILogger<PaymentOrchestrator> log)
    {
        _db = db;
        _router = router;
        _applier = applier;
        _outbox = outbox;
        _references = references;
        _clock = clock;
        _log = log;
    }

    // ---- Initiate ----

    public async Task<PaymentResult<InitiatedPayment>> InitiateAsync(
        InitiatePaymentCommand command, CancellationToken ct = default)
    {
        if (!Currencies.IsWellFormed(command.Currency))
            return PaymentResult<InitiatedPayment>.Fail(PaymentProblem.InvalidCurrency, "Currency must be a three-letter code.");

        var currency = Currencies.Normalize(command.Currency);

        if (command.Amount <= 0)
            return PaymentResult<InitiatedPayment>.Fail(PaymentProblem.InvalidAmount, "Amount must be greater than zero.");

        Money amount;
        try
        {
            amount = Money.FromDecimal(command.Amount, currency);
        }
        catch (ArgumentOutOfRangeException)
        {
            return PaymentResult<InitiatedPayment>.Fail(PaymentProblem.InvalidAmount, "Amount is out of range.");
        }

        if (!amount.IsPositive)
            return PaymentResult<InitiatedPayment>.Fail(
                PaymentProblem.InvalidAmount,
                $"{command.Amount} rounds to zero in {currency}.");

        if (ValidateMetadata(command.Metadata) is { } metadataProblem)
            return PaymentResult<InitiatedPayment>.Fail(PaymentProblem.MetadataTooLarge, metadataProblem);

        foreach (var (name, url) in new[]
                 {
                     ("returnUrl", command.ReturnUrl),
                     ("cancelUrl", command.CancelUrl),
                     ("callbackUrl", command.CallbackUrl)
                 })
        {
            if (url is not null && !IsAbsoluteHttpUrl(url))
                return PaymentResult<InitiatedPayment>.Fail(
                    PaymentProblem.InvalidReturnUrl, $"{name} must be an absolute http(s) URL.");
        }

        // Safety net beneath the HTTP idempotency filter, for callers that reach the orchestrator
        // another way. A key that already produced a registered or finished payment returns it;
        // one whose payment never reached the gateway re-drives the call instead, so a create that
        // failed on a timeout can be retried with the same key and the same effect.
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await Query(command.MerchantId)
                .FirstOrDefaultAsync(p => p.IdempotencyKey == command.IdempotencyKey, ct);

            if (existing is not null)
            {
                if (existing.GatewayPaymentId is not null || existing.Status.IsTerminal())
                    return PaymentResult<InitiatedPayment>.Ok(new InitiatedPayment(existing, CustomerAction.None));

                return await RegisterAsync(existing, ct);
            }
        }

        var routed = await _router.RouteNewAsync(command.MerchantId, currency, command.GatewayKey, ct);
        if (!routed.IsSuccess)
            return PaymentResult<InitiatedPayment>.Fail(routed.Problem, routed.Detail);

        var (gateway, account, _) = routed.Value!;

        if (command.CaptureMode == CaptureMode.Manual && !gateway.Capabilities.HasFlag(GatewayCapabilities.ManualCapture))
            return PaymentResult<InitiatedPayment>.Fail(
                PaymentProblem.CapabilityNotSupported,
                $"{gateway.DisplayName} cannot authorize without capturing.");

        var now = _clock.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = command.MerchantId,
            Reference = _references.NewPaymentReference(),
            OrderId = command.OrderId,
            OrderNumber = command.OrderNumber,
            Description = command.Description,
            GatewayKey = gateway.Key,
            GatewayAccountId = account.Id,
            Currency = currency,
            AmountMinor = amount.MinorUnits,
            CaptureMode = command.CaptureMode,
            Status = PaymentStatus.Pending,
            CustomerId = command.Customer?.Id,
            CustomerName = command.Customer?.Name,
            CustomerEmail = command.Customer?.Email,
            CustomerPhone = command.Customer?.Phone,
            ReturnUrl = command.ReturnUrl,
            CancelUrl = command.CancelUrl,
            CallbackUrl = command.CallbackUrl,
            Metadata = command.Metadata is null ? new() : new Dictionary<string, string>(command.Metadata),
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        return await RegisterAsync(payment, ct);
    }

    /// <summary>Calls the gateway for a payment that exists locally but is not registered yet.</summary>
    private async Task<PaymentResult<InitiatedPayment>> RegisterAsync(Payment payment, CancellationToken ct)
    {
        var routed = await _router.RouteExistingAsync(payment, ct);
        if (!routed.IsSuccess)
            return PaymentResult<InitiatedPayment>.Fail(routed.Problem, routed.Detail);

        var (gateway, _, context) = routed.Value!;

        var request = new GatewayCreatePaymentRequest(
            context,
            payment.Id,
            payment.Reference,
            payment.Amount,
            payment.CaptureMode,
            payment.Description,
            new GatewayCustomer(payment.CustomerId, payment.CustomerName, payment.CustomerEmail, payment.CustomerPhone),
            payment.ReturnUrl,
            payment.CancelUrl,
            payment.Metadata,
            _references.GatewayIdempotencyKey(payment.Id, "create"));

        var (result, attempt) = await CallAsync(
            payment, PaymentOperation.Create, request.IdempotencyKey,
            () => gateway.CreatePaymentAsync(request, ct), ct);

        if (!result.IsSuccess)
        {
            await FinishFailureAsync(payment, result.Outcome, result.Error, ct);
            return PaymentResult<InitiatedPayment>.Fail(ProblemFor(result.Outcome), result.Error?.Message, result.Error);
        }

        var snapshot = result.Value!;
        attempt.GatewayReference = snapshot.GatewayPaymentId;

        var now = _clock.UtcNow;
        var change = _applier.ApplyPayment(payment, snapshot, now);

        _outbox.EnqueuePayment(payment, PaymentEventTypes.Created, now);
        if (change.StatusChanged && _outbox.EventTypeFor(payment.Status) is { } eventType)
            _outbox.EnqueuePayment(payment, eventType, now);

        await _db.SaveChangesAsync(ct);

        return PaymentResult<InitiatedPayment>.Ok(
            new InitiatedPayment(payment, snapshot.Action ?? CustomerAction.None));
    }

    // ---- Capture ----

    public async Task<PaymentResult<Payment>> CaptureAsync(
        Guid merchantId, Guid paymentId, CapturePaymentCommand command, CancellationToken ct = default)
    {
        var payment = await LoadAsync(merchantId, paymentId, ct);
        if (payment is null) return PaymentResult<Payment>.Fail(PaymentProblem.PaymentNotFound);

        if (!payment.Status.CanCapture())
            return PaymentResult<Payment>.Fail(
                PaymentProblem.InvalidState, $"A payment that is {payment.Status} cannot be captured.");

        var routed = await _router.RouteExistingAsync(payment, ct);
        if (!routed.IsSuccess) return PaymentResult<Payment>.Fail(routed.Problem, routed.Detail);

        var (gateway, _, context) = routed.Value!;

        var remaining = payment.CapturableRemaining;
        var amount = remaining;

        if (command.Amount is { } requested && !TryMoney(requested, payment.Currency, out amount))
            return PaymentResult<Payment>.Fail(PaymentProblem.InvalidAmount, "Capture amount is out of range.");

        if (!amount.IsPositive)
            return PaymentResult<Payment>.Fail(PaymentProblem.InvalidAmount, "Capture amount must be greater than zero.");

        if (amount > remaining)
            return PaymentResult<Payment>.Fail(
                PaymentProblem.CaptureExceedsAuthorized,
                $"Only {remaining} of this authorization is left to capture.");

        if (amount < remaining && !gateway.Capabilities.HasFlag(GatewayCapabilities.PartialCapture))
            return PaymentResult<Payment>.Fail(
                PaymentProblem.CapabilityNotSupported, $"{gateway.DisplayName} cannot capture a partial amount.");

        var key = _references.GatewayIdempotencyKey(payment.Id, "capture");
        var request = new GatewayCaptureRequest(context, payment.GatewayPaymentId!, amount, key);

        var (result, attempt) = await CallAsync(
            payment, PaymentOperation.Capture, key, () => gateway.CapturePaymentAsync(request, ct), ct);

        return await FinishAsync(payment, result, attempt, ct);
    }

    // ---- Cancel ----

    public async Task<PaymentResult<Payment>> CancelAsync(
        Guid merchantId, Guid paymentId, CancelPaymentCommand command, CancellationToken ct = default)
    {
        var payment = await LoadAsync(merchantId, paymentId, ct);
        if (payment is null) return PaymentResult<Payment>.Fail(PaymentProblem.PaymentNotFound);

        if (!payment.Status.CanCancel())
            return PaymentResult<Payment>.Fail(
                PaymentProblem.InvalidState, $"A payment that is {payment.Status} cannot be cancelled.");

        var now = _clock.UtcNow;

        // Never reached a gateway, so there is nothing out there to void. Cancelling locally is
        // both correct and the only option — calling a gateway with no id would just error.
        if (payment.GatewayPaymentId is null)
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.CancelledAt = now;
            payment.UpdatedAt = now;
            payment.FailureCode ??= "cancelled";
            payment.FailureMessage ??= command.Reason;

            _outbox.EnqueuePayment(payment, PaymentEventTypes.Cancelled, now);
            await _db.SaveChangesAsync(ct);
            return PaymentResult<Payment>.Ok(payment);
        }

        var routed = await _router.RouteExistingAsync(payment, ct);
        if (!routed.IsSuccess) return PaymentResult<Payment>.Fail(routed.Problem, routed.Detail);

        var (gateway, _, context) = routed.Value!;

        if (!gateway.Capabilities.HasFlag(GatewayCapabilities.Cancellation))
            return PaymentResult<Payment>.Fail(
                PaymentProblem.CapabilityNotSupported, $"{gateway.DisplayName} cannot cancel a payment.");

        var key = _references.GatewayIdempotencyKey(payment.Id, "cancel");
        var request = new GatewayCancelRequest(context, payment.GatewayPaymentId, command.Reason, key);

        var (result, attempt) = await CallAsync(
            payment, PaymentOperation.Cancel, key, () => gateway.CancelPaymentAsync(request, ct), ct);

        return await FinishAsync(payment, result, attempt, ct);
    }

    // ---- Refund ----

    public async Task<PaymentResult<Refund>> RefundAsync(
        Guid merchantId, Guid paymentId, RefundPaymentCommand command, CancellationToken ct = default)
    {
        var payment = await LoadAsync(merchantId, paymentId, ct);
        if (payment is null) return PaymentResult<Refund>.Fail(PaymentProblem.PaymentNotFound);

        if (!payment.Status.CanRefund())
            return PaymentResult<Refund>.Fail(
                PaymentProblem.InvalidState, $"A payment that is {payment.Status} cannot be refunded.");

        if (ValidateMetadata(command.Metadata) is { } metadataProblem)
            return PaymentResult<Refund>.Fail(PaymentProblem.MetadataTooLarge, metadataProblem);

        var routed = await _router.RouteExistingAsync(payment, ct);
        if (!routed.IsSuccess) return PaymentResult<Refund>.Fail(routed.Problem, routed.Detail);

        var (gateway, _, context) = routed.Value!;

        if (!gateway.Capabilities.HasFlag(GatewayCapabilities.Refunds))
            return PaymentResult<Refund>.Fail(
                PaymentProblem.CapabilityNotSupported, $"{gateway.DisplayName} cannot process refunds.");

        var remaining = payment.RefundableRemaining;
        var amount = remaining;

        if (command.Amount is { } requested && !TryMoney(requested, payment.Currency, out amount))
            return PaymentResult<Refund>.Fail(PaymentProblem.InvalidAmount, "Refund amount is out of range.");

        if (!amount.IsPositive)
            return PaymentResult<Refund>.Fail(PaymentProblem.InvalidAmount, "Refund amount must be greater than zero.");

        if (amount > remaining)
            return PaymentResult<Refund>.Fail(
                PaymentProblem.RefundExceedsCaptured, $"Only {remaining} of this payment is left to refund.");

        if (amount < remaining && !gateway.Capabilities.HasFlag(GatewayCapabilities.PartialRefunds))
            return PaymentResult<Refund>.Fail(
                PaymentProblem.CapabilityNotSupported, $"{gateway.DisplayName} cannot refund a partial amount.");

        var now = _clock.UtcNow;

        // Its own row, created and committed before the call, for the same reason attempts are:
        // the gateway may take the money back and never tell us, and an untracked refund is money
        // that has left without a record.
        var refund = new Refund
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Reference = _references.NewRefundReference(),
            Currency = payment.Currency,
            AmountMinor = amount.MinorUnits,
            Status = RefundStatus.Pending,
            Reason = command.Reason,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        payment.Refunds.Add(refund);
        _db.Refunds.Add(refund);
        await _db.SaveChangesAsync(ct);

        var key = _references.GatewayIdempotencyKey(refund.Id, "refund");
        var request = new GatewayRefundRequest(
            context,
            payment.GatewayPaymentId!,
            refund.Id,
            refund.Reference,
            amount,
            command.Reason,
            command.Metadata ?? new Dictionary<string, string>(),
            key);

        var (result, attempt) = await CallAsync(
            payment, PaymentOperation.Refund, key, () => gateway.RefundAsync(request, ct), ct, refund.Id);

        var completedAt = _clock.UtcNow;

        if (!result.IsSuccess)
        {
            // A refund we are not sure about stays Pending — calling it failed would invite a
            // second refund and hand the customer their money twice.
            refund.Status = result.Outcome == AttemptOutcome.Unknown ? RefundStatus.Pending : RefundStatus.Failed;
            refund.FailureCode = result.Error?.Code;
            refund.FailureMessage = result.Error?.Message;
            refund.UpdatedAt = completedAt;

            if (refund.Status == RefundStatus.Failed)
                _outbox.EnqueueRefund(payment, refund, PaymentEventTypes.RefundFailed, completedAt);

            await _db.SaveChangesAsync(ct);
            return PaymentResult<Refund>.Fail(ProblemFor(result.Outcome), result.Error?.Message, result.Error);
        }

        attempt.GatewayReference = result.Value!.GatewayRefundId;
        _applier.ApplyRefund(payment, refund, result.Value, completedAt);

        _outbox.EnqueueRefund(payment, refund, PaymentEventTypes.RefundCreated, completedAt);
        if (refund.Status == RefundStatus.Succeeded)
            _outbox.EnqueueRefund(payment, refund, PaymentEventTypes.RefundSucceeded, completedAt);

        await _db.SaveChangesAsync(ct);
        return PaymentResult<Refund>.Ok(refund);
    }

    // ---- Sync ----

    public async Task<PaymentResult<Payment>> SyncAsync(Guid merchantId, Guid paymentId, CancellationToken ct = default)
    {
        var payment = await LoadAsync(merchantId, paymentId, ct);
        if (payment is null) return PaymentResult<Payment>.Fail(PaymentProblem.PaymentNotFound);

        if (payment.GatewayPaymentId is null)
            return PaymentResult<Payment>.Fail(
                PaymentProblem.InvalidState, "This payment was never registered with a gateway.");

        var routed = await _router.RouteExistingAsync(payment, ct);
        if (!routed.IsSuccess) return PaymentResult<Payment>.Fail(routed.Problem, routed.Detail);

        var (gateway, _, context) = routed.Value!;

        if (!gateway.Capabilities.HasFlag(GatewayCapabilities.StatusPolling))
            return PaymentResult<Payment>.Fail(
                PaymentProblem.CapabilityNotSupported, $"{gateway.DisplayName} cannot be polled for status.");

        var request = new GatewayFetchRequest(context, payment.GatewayPaymentId);

        var (result, attempt) = await CallAsync(
            payment, PaymentOperation.Fetch, null, () => gateway.FetchPaymentAsync(request, ct), ct);

        return await FinishAsync(payment, result, attempt, ct);
    }

    // ---- Reads ----

    public Task<Payment?> GetAsync(Guid merchantId, Guid paymentId, CancellationToken ct = default) =>
        Query(merchantId).Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public Task<Payment?> GetByReferenceAsync(Guid merchantId, string reference, CancellationToken ct = default) =>
        Query(merchantId).Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Reference == reference, ct);

    public async Task<PagedResult<Payment>> ListAsync(PaymentQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var payments = Query(query.MerchantId);

        if (query.OrderId is { } orderId) payments = payments.Where(p => p.OrderId == orderId);
        if (query.CustomerId is { } customerId) payments = payments.Where(p => p.CustomerId == customerId);
        if (query.Status is { } status) payments = payments.Where(p => p.Status == status);
        if (query.From is { } from) payments = payments.Where(p => p.CreatedAt >= from);
        if (query.To is { } to) payments = payments.Where(p => p.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(query.GatewayKey))
            payments = payments.Where(p => p.GatewayKey == query.GatewayKey);

        var total = await payments.CountAsync(ct);

        var items = await payments
            .Include(p => p.Refunds)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Payment>(items, page, pageSize, total);
    }

    // ---- Shared machinery ----

    /// <summary>
    /// Every read is scoped to the merchant. Not a convenience — it is the tenant boundary, and a
    /// single query that forgets it leaks one shop's payments to another.
    /// </summary>
    private IQueryable<Payment> Query(Guid merchantId) =>
        _db.Payments.Where(p => p.MerchantId == merchantId);

    private Task<Payment?> LoadAsync(Guid merchantId, Guid paymentId, CancellationToken ct) =>
        Query(merchantId)
            .Include(p => p.Refunds)
            .Include(p => p.GatewayAccount)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    /// <summary>
    /// Writes the attempt, commits it, makes the call, and records the answer.
    /// <para>
    /// The commit before the call is what makes a crash survivable: without it, a process that dies
    /// while a gateway is charging a customer leaves no trace that it ever asked. An exception from
    /// the integration is recorded as <see cref="AttemptOutcome.Unknown"/> rather than a failure,
    /// because a request that threw may still have been received.
    /// </para>
    /// </summary>
    private async Task<(GatewayResult<T> Result, PaymentAttempt Attempt)> CallAsync<T>(
        Payment payment,
        PaymentOperation operation,
        string? idempotencyKey,
        Func<Task<GatewayResult<T>>> call,
        CancellationToken ct,
        Guid? refundId = null)
        where T : class
    {
        var startedAt = _clock.UtcNow;

        var attempt = new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            RefundId = refundId,
            Operation = operation,
            Outcome = AttemptOutcome.Started,
            GatewayIdempotencyKey = idempotencyKey,
            StartedAt = startedAt
        };

        _db.PaymentAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        GatewayResult<T> result;
        try
        {
            result = await call();
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Gateway {Gateway} threw during {Operation} for payment {PaymentId}.",
                payment.GatewayKey, operation, payment.Id);

            result = GatewayResult<T>.Unknown(new GatewayError(
                GatewayErrorCodes.Unknown, "The gateway integration threw before returning a result."));
        }

        var completedAt = _clock.UtcNow;
        attempt.Outcome = result.Outcome;
        attempt.CompletedAt = completedAt;
        attempt.DurationMs = (int)Math.Min(int.MaxValue, (completedAt - startedAt).TotalMilliseconds);
        attempt.ErrorCode = result.Error?.Code;
        attempt.ErrorMessage = Truncate(result.Error?.Message, 2000);
        attempt.ResponseSnapshot = Truncate(result.RawResponse, MaxResponseSnapshot);

        return (result, attempt);
    }

    /// <summary>Applies a successful snapshot, or records the failure, and saves either way.</summary>
    private async Task<PaymentResult<Payment>> FinishAsync(
        Payment payment, GatewayResult<GatewayPaymentSnapshot> result, PaymentAttempt attempt, CancellationToken ct)
    {
        if (!result.IsSuccess)
        {
            await FinishFailureAsync(payment, result.Outcome, result.Error, ct);
            return PaymentResult<Payment>.Fail(ProblemFor(result.Outcome), result.Error?.Message, result.Error);
        }

        var snapshot = result.Value!;
        attempt.GatewayReference = snapshot.GatewayPaymentId;

        var now = _clock.UtcNow;
        var change = _applier.ApplyPayment(payment, snapshot, now);

        if (change.StatusChanged && _outbox.EventTypeFor(payment.Status) is { } eventType)
            _outbox.EnqueuePayment(payment, eventType, now);

        await _db.SaveChangesAsync(ct);
        return PaymentResult<Payment>.Ok(payment);
    }

    /// <summary>
    /// A decline or a rejection is a final answer, so the payment becomes <c>Failed</c>. Anything
    /// unreachable or unknown leaves the payment where it is — the reconciler resolves it against
    /// the gateway, which is the only source that knows whether money moved.
    /// </summary>
    private async Task FinishFailureAsync(
        Payment payment, AttemptOutcome outcome, GatewayError? error, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        payment.UpdatedAt = now;
        payment.FailureCode = error?.Code;
        payment.FailureMessage = Truncate(error?.Message, 1000);

        var terminal = outcome is
            AttemptOutcome.Declined or AttemptOutcome.Rejected or AttemptOutcome.PermanentFailure;

        if (terminal && payment.Status.CanTransitionTo(PaymentStatus.Failed))
        {
            payment.Status = PaymentStatus.Failed;
            _outbox.EnqueuePayment(payment, PaymentEventTypes.Failed, now);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static PaymentProblem ProblemFor(AttemptOutcome outcome) => outcome switch
    {
        AttemptOutcome.Declined => PaymentProblem.GatewayDeclined,
        AttemptOutcome.TransientFailure => PaymentProblem.GatewayUnavailable,
        AttemptOutcome.Unknown => PaymentProblem.GatewayOutcomeUnknown,
        _ => PaymentProblem.GatewayRejected
    };

    private static string? ValidateMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return null;

        if (metadata.Count > MaxMetadataEntries)
            return $"Metadata may hold at most {MaxMetadataEntries} entries.";

        foreach (var (key, value) in metadata)
        {
            if (key.Length > MaxMetadataKeyLength)
                return $"Metadata key '{key[..Math.Min(key.Length, 20)]}…' exceeds {MaxMetadataKeyLength} characters.";
            if (value.Length > MaxMetadataValueLength)
                return $"Metadata value for '{key}' exceeds {MaxMetadataValueLength} characters.";
        }

        return null;
    }

    private static bool TryMoney(decimal amount, string currency, out Money money)
    {
        try
        {
            money = Money.FromDecimal(amount, currency);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            money = Money.Zero(currency);
            return false;
        }
    }

    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
