using Microsoft.Extensions.Logging;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Infrastructure.Payments;

public sealed record PaymentStateChange(PaymentStatus Previous, PaymentStatus Current)
{
    public bool StatusChanged => Previous != Current;
}

/// <summary>
/// Folds a gateway snapshot into a stored payment.
/// <para>
/// One implementation shared by the orchestrator and the webhook ingestor, because a payment
/// reaching <c>Captured</c> through a webhook and through a poll must end up in the same state.
/// Two copies of this logic drift, and the way you find out is a payment that reads as captured on
/// one path and pending on the other.
/// </para>
/// </summary>
public interface IPaymentStateApplier
{
    PaymentStateChange ApplyPayment(Payment payment, GatewayPaymentSnapshot snapshot, DateTime now);

    /// <summary>Returns true when the refund's status changed.</summary>
    bool ApplyRefund(Payment payment, Refund refund, GatewayRefundSnapshot snapshot, DateTime now);
}

public sealed class PaymentStateApplier : IPaymentStateApplier
{
    private readonly ILogger<PaymentStateApplier> _log;

    public PaymentStateApplier(ILogger<PaymentStateApplier> log) => _log = log;

    public PaymentStateChange ApplyPayment(Payment payment, GatewayPaymentSnapshot snapshot, DateTime now)
    {
        var previous = payment.Status;

        payment.GatewayPaymentId ??= snapshot.GatewayPaymentId;
        if (snapshot.GatewayStatusCode is not null) payment.GatewayStatusCode = snapshot.GatewayStatusCode;
        if (snapshot.ExpiresAt is not null) payment.ExpiresAt = snapshot.ExpiresAt;

        // Null means "not reported", which is not the same as zero. Overwriting a known captured
        // amount with zero because this particular notification did not mention it would un-pay
        // the order.
        payment.AuthorizedMinor = Amount(payment, snapshot.AuthorizedAmount, payment.AuthorizedMinor);
        payment.CapturedMinor = Amount(payment, snapshot.CapturedAmount, payment.CapturedMinor);
        payment.RefundedMinor = Amount(payment, snapshot.RefundedAmount, payment.RefundedMinor);

        if (snapshot.Method is { } method)
        {
            payment.MethodKind = method.Kind;
            payment.MethodBrand = method.Brand ?? payment.MethodBrand;
            payment.MethodLast4 = method.Last4 ?? payment.MethodLast4;
        }

        if (snapshot.FailureCode is not null) payment.FailureCode = snapshot.FailureCode;
        if (snapshot.FailureMessage is not null) payment.FailureMessage = snapshot.FailureMessage;

        // Gateways vary most in how they describe partial refunds, so that part is derived from the
        // amounts rather than believed.
        var target = snapshot.Status == PaymentStatus.Captured && payment.RefundedMinor > 0
            ? PaymentStatuses.AfterRefund(payment.CapturedMinor, payment.RefundedMinor)
            : snapshot.Status;

        if (previous.CanTransitionTo(target))
        {
            payment.Status = target;
            Stamp(payment, target, now);
        }
        else
        {
            // Expected, not exceptional: webhooks are retried with backoff, so an older delivery
            // routinely lands after a newer one. Logged because a persistent stream of these
            // means an integration is mapping statuses wrongly.
            _log.LogInformation(
                "Ignored out-of-order status {Target} for payment {PaymentId}; it is already {Current}.",
                target, payment.Id, previous);
        }

        payment.UpdatedAt = now;
        return new PaymentStateChange(previous, payment.Status);
    }

    public bool ApplyRefund(Payment payment, Refund refund, GatewayRefundSnapshot snapshot, DateTime now)
    {
        var previous = refund.Status;

        refund.GatewayRefundId ??= snapshot.GatewayRefundId;
        if (snapshot.GatewayStatusCode is not null) refund.GatewayStatusCode = snapshot.GatewayStatusCode;
        if (snapshot.FailureCode is not null) refund.FailureCode = snapshot.FailureCode;
        if (snapshot.FailureMessage is not null) refund.FailureMessage = snapshot.FailureMessage;

        // A refund that already succeeded is not walked back by a late delivery.
        if (previous is not (RefundStatus.Succeeded or RefundStatus.Failed) || snapshot.Status == previous)
            refund.Status = snapshot.Status;

        if (refund.Status == RefundStatus.Succeeded && refund.SettledAt is null)
            refund.SettledAt = snapshot.SettledAt ?? now;

        refund.UpdatedAt = now;

        // The payment's refunded total is recomputed from the refunds that actually succeeded,
        // never incremented in place — an increment applied twice by a redelivered webhook would
        // report more given back than was ever taken.
        var succeeded = payment.Refunds
            .Where(r => r.Status == RefundStatus.Succeeded)
            .Sum(r => r.AmountMinor);

        payment.RefundedMinor = succeeded;

        var target = PaymentStatuses.AfterRefund(payment.CapturedMinor, succeeded);
        if (payment.Status.CanTransitionTo(target)) payment.Status = target;

        payment.UpdatedAt = now;
        return previous != refund.Status;
    }

    /// <summary>Keeps a reported amount only when it is reported, and only in the payment's own currency.</summary>
    private long Amount(Payment payment, Money? reported, long current)
    {
        if (reported is not { } money) return current;

        if (!string.Equals(money.Currency, payment.Currency, StringComparison.Ordinal))
        {
            _log.LogError(
                "Gateway {Gateway} reported {Reported} for payment {PaymentId}, which is in {Currency}. Amount ignored.",
                payment.GatewayKey, money, payment.Id, payment.Currency);
            return current;
        }

        return money.MinorUnits;
    }

    private static void Stamp(Payment payment, PaymentStatus status, DateTime now)
    {
        switch (status)
        {
            case PaymentStatus.Authorized:
                payment.AuthorizedAt ??= now;
                break;
            case PaymentStatus.Captured:
            case PaymentStatus.PartiallyRefunded:
            case PaymentStatus.Refunded:
                payment.AuthorizedAt ??= now;
                payment.CapturedAt ??= now;
                break;
            case PaymentStatus.Cancelled:
                payment.CancelledAt ??= now;
                break;
        }
    }
}
