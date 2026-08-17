using System.Collections.Concurrent;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Common;
using PaymentService.Domain.Enums;

namespace PaymentService.Infrastructure.Gateways.Sandbox;

/// <summary>
/// A fake gateway — <b>not</b> an integration with any payment provider.
/// <para>
/// It exists so the port has a reference implementation: it shows what a real integration has to
/// return, and it lets the whole service be exercised end to end before a provider is chosen.
/// It is registered only when <c>Gateways:sandbox:Enabled</c> is true, which the production
/// settings do not set. Delete it once a real gateway lands, or keep it for tests.
/// </para>
/// <para>
/// State lives in memory, so it is lost on restart and not shared between instances. That is fine
/// for its purpose and is the main reason it must never be enabled in production.
/// </para>
/// </summary>
public sealed class SandboxPaymentGateway : IPaymentGateway
{
    /// <summary>Amounts whose minor units end in these values fail, so declines can be exercised on demand.</summary>
    private const int DeclineSuffix = 51;
    private const int ErrorSuffix = 52;

    private readonly ConcurrentDictionary<string, SandboxPayment> _payments = new();

    public string Key => "sandbox";
    public string DisplayName => "Sandbox (test double)";

    public GatewayCapabilities Capabilities =>
        GatewayCapabilities.ManualCapture |
        GatewayCapabilities.PartialCapture |
        GatewayCapabilities.Refunds |
        GatewayCapabilities.PartialRefunds |
        GatewayCapabilities.Cancellation |
        GatewayCapabilities.StatusPolling |
        GatewayCapabilities.HostedRedirect;

    public IReadOnlyCollection<string> SupportedCurrencies => Array.Empty<string>();

    public Task<GatewayResult<GatewayPaymentSnapshot>> CreatePaymentAsync(
        GatewayCreatePaymentRequest request, CancellationToken ct = default)
    {
        var suffix = (int)(request.Amount.MinorUnits % 100);

        if (suffix == DeclineSuffix)
            return Task.FromResult(GatewayResult<GatewayPaymentSnapshot>.Declined(
                new GatewayError(GatewayErrorCodes.InsufficientFunds, "Sandbox decline.", "sandbox_declined")));

        if (suffix == ErrorSuffix)
            return Task.FromResult(GatewayResult<GatewayPaymentSnapshot>.Transient(
                new GatewayError(GatewayErrorCodes.Unavailable, "Sandbox outage.", "sandbox_unavailable")));

        // Idempotent by key, the way a real gateway is: the same key returns the same payment
        // rather than creating a second one.
        var existing = _payments.Values.FirstOrDefault(p => p.IdempotencyKey == request.IdempotencyKey);
        if (existing is not null) return Task.FromResult(Ok(existing));

        var payment = new SandboxPayment
        {
            Id = $"sbx_{Guid.NewGuid():N}"[..24],
            IdempotencyKey = request.IdempotencyKey,
            Currency = request.Amount.Currency,
            AmountMinor = request.Amount.MinorUnits,
            CaptureMode = request.CaptureMode,
            ReturnUrl = request.ReturnUrl,
            Status = PaymentStatus.RequiresAction
        };

        _payments[payment.Id] = payment;
        return Task.FromResult(Ok(payment));
    }

    public Task<GatewayResult<GatewayPaymentSnapshot>> CapturePaymentAsync(
        GatewayCaptureRequest request, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(request.GatewayPaymentId, out var payment)) return Task.FromResult(NotFound());

        payment.CapturedMinor += request.Amount.MinorUnits;
        payment.Status = PaymentStatus.Captured;
        return Task.FromResult(Ok(payment));
    }

    public Task<GatewayResult<GatewayPaymentSnapshot>> CancelPaymentAsync(
        GatewayCancelRequest request, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(request.GatewayPaymentId, out var payment)) return Task.FromResult(NotFound());

        payment.Status = PaymentStatus.Cancelled;
        return Task.FromResult(Ok(payment));
    }

    public Task<GatewayResult<GatewayPaymentSnapshot>> FetchPaymentAsync(
        GatewayFetchRequest request, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(request.GatewayPaymentId, out var payment)) return Task.FromResult(NotFound());

        return Task.FromResult(Ok(payment));
    }

    public Task<GatewayResult<GatewayRefundSnapshot>> RefundAsync(
        GatewayRefundRequest request, CancellationToken ct = default)
    {
        if (!_payments.TryGetValue(request.GatewayPaymentId, out var payment))
            return Task.FromResult(GatewayResult<GatewayRefundSnapshot>.Rejected(
                new GatewayError(GatewayErrorCodes.InvalidRequest, "No such sandbox payment.")));

        payment.RefundedMinor += request.Amount.MinorUnits;

        return Task.FromResult(GatewayResult<GatewayRefundSnapshot>.Success(new GatewayRefundSnapshot(
            $"sbxr_{request.RefundId:N}"[..24],
            RefundStatus.Succeeded,
            request.Amount,
            "sandbox_refunded",
            SettledAt: DateTime.UtcNow)));
    }

    /// <summary>
    /// Confirms a sandbox payment, standing in for the customer completing a hosted checkout.
    /// Called by the sandbox controller; a real gateway does this on its own site.
    /// </summary>
    public bool Confirm(string gatewayPaymentId, bool success)
    {
        if (!_payments.TryGetValue(gatewayPaymentId, out var payment)) return false;

        if (!success)
        {
            payment.Status = PaymentStatus.Failed;
            return true;
        }

        payment.Status = payment.CaptureMode == CaptureMode.Manual
            ? PaymentStatus.Authorized
            : PaymentStatus.Captured;

        if (payment.Status == PaymentStatus.Captured) payment.CapturedMinor = payment.AmountMinor;

        return true;
    }

    /// <summary>Where to send the customer after the fake checkout, mirroring a hosted redirect.</summary>
    public string? ReturnUrlOf(string gatewayPaymentId) =>
        _payments.TryGetValue(gatewayPaymentId, out var payment) ? payment.ReturnUrl : null;

    private static GatewayResult<GatewayPaymentSnapshot> NotFound() =>
        GatewayResult<GatewayPaymentSnapshot>.Rejected(
            new GatewayError(GatewayErrorCodes.InvalidRequest, "No such sandbox payment."));

    private static GatewayResult<GatewayPaymentSnapshot> Ok(SandboxPayment payment)
    {
        var action = payment.Status == PaymentStatus.RequiresAction
            ? new CustomerAction(
                CustomerActionKind.Redirect,
                $"/api/sandbox/checkout/{payment.Id}",
                "GET",
                ExpiresAt: DateTime.UtcNow.AddMinutes(30))
            : CustomerAction.None;

        return GatewayResult<GatewayPaymentSnapshot>.Success(new GatewayPaymentSnapshot(
            payment.Id,
            payment.Status,
            AuthorizedAmount: payment.Status is PaymentStatus.Authorized or PaymentStatus.Captured
                ? Money.FromMinorUnits(payment.AmountMinor, payment.Currency)
                : null,
            CapturedAmount: Money.FromMinorUnits(payment.CapturedMinor, payment.Currency),
            RefundedAmount: Money.FromMinorUnits(payment.RefundedMinor, payment.Currency),
            Method: new GatewayPaymentMethod(PaymentMethodKind.Card, "Sandbox", "4242"),
            Action: action,
            GatewayStatusCode: payment.Status.ToString().ToLowerInvariant()));
    }

    private sealed class SandboxPayment
    {
        public required string Id { get; init; }
        public required string IdempotencyKey { get; init; }
        public required string Currency { get; init; }
        public required long AmountMinor { get; init; }
        public required CaptureMode CaptureMode { get; init; }
        public string? ReturnUrl { get; init; }
        public PaymentStatus Status { get; set; }
        public long CapturedMinor { get; set; }
        public long RefundedMinor { get; set; }
    }
}
