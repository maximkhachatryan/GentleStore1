using System.Globalization;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Enums;

namespace PaymentService.Infrastructure.Gateways.Paynet;

/// <summary>
/// Paynet's vocabulary translated into this service's. Everything provider-specific about naming,
/// statuses and payment methods lives here.
/// </summary>
internal static class PaynetMapping
{
    public static string GatewayKey(string processor) => $"paynet-{processor.Trim().ToLowerInvariant()}";

    public static string DisplayName(string processor) => processor.Trim().ToLowerInvariant() switch
    {
        "arca" => "Paynet - ARCA / bank card",
        "idram" => "Paynet - Idram",
        "telcell" => "Paynet - Telcell",
        var other => $"Paynet - {other}"
    };

    public static PaymentMethodKind MethodKind(string? processor) => processor?.Trim().ToLowerInvariant() switch
    {
        "arca" => PaymentMethodKind.Card,
        "idram" or "telcell" => PaymentMethodKind.Wallet,
        _ => PaymentMethodKind.Other
    };

    /// <summary>
    /// Paynet's <c>TransactionStatus</c>: pending, processing, completed, failed, expired, refunded.
    /// <para>
    /// An unrecognised value maps to <see cref="PaymentStatus.Processing"/> — "something is
    /// happening, keep waiting". That is the only safe default: the transition rules refuse a move
    /// backwards from any terminal status, so a status Paynet adds later can never un-pay a
    /// captured payment, and the reconciler keeps polling until it means something.
    /// </para>
    /// </summary>
    public static PaymentStatus ToStatus(string? status, out bool recognised)
    {
        recognised = true;

        switch (status?.Trim().ToLowerInvariant())
        {
            // Paynet hands back a checkout URL immediately, so a pending payment is one waiting on
            // the customer rather than one waiting on us.
            case "pending": return PaymentStatus.RequiresAction;
            case "processing": return PaymentStatus.Processing;
            case "completed": return PaymentStatus.Captured;
            case "failed": return PaymentStatus.Failed;
            case "expired": return PaymentStatus.Expired;
            case "refunded": return PaymentStatus.Refunded;
            default:
                recognised = false;
                return PaymentStatus.Processing;
        }
    }

    public static RefundStatus ToRefundStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "completed" or "succeeded" or "refunded" => RefundStatus.Succeeded,
        "failed" or "rejected" => RefundStatus.Failed,
        "cancelled" or "canceled" or "reversed" => RefundStatus.Cancelled,
        "pending" => RefundStatus.Pending,

        // Same reasoning as payments: an unknown refund status is treated as still in flight, never
        // as failed, because a refund wrongly marked failed invites a second one and pays the
        // customer back twice.
        _ => RefundStatus.Processing
    };

    /// <summary>Snapshot from a REST payment resource, which reports amounts authoritatively.</summary>
    public static GatewayPaymentSnapshot ToSnapshot(PaynetPayment payment, string processor, out bool recognised)
    {
        var status = ToStatus(payment.Status, out recognised);
        var settled = status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded;

        return new GatewayPaymentSnapshot(
            payment.Uuid ?? string.Empty,
            status,
            // Paynet has no separate authorization step, so a settled payment was authorized for
            // the same amount at the same moment. Left null before settlement: null means "not
            // reported", and reporting zero would erase what we already know.
            AuthorizedAmount: settled ? PaynetAmounts.FromDram(payment.Amount) : null,
            CapturedAmount: settled ? PaynetAmounts.FromDram(payment.Amount) : null,
            RefundedAmount: PaynetAmounts.FromDram(payment.RefundedAmount),
            Method: new GatewayPaymentMethod(MethodKind(payment.Processor ?? processor), payment.Processor ?? processor),
            Action: ToAction(payment),
            GatewayStatusCode: payment.Status,
            FailureCode: status == PaymentStatus.Failed ? payment.Status : null,
            FailureMessage: status == PaymentStatus.Failed ? "The processor declined the payment." : null);
    }

    /// <summary>
    /// Snapshot from a webhook, whose <c>transaction</c> object is much thinner than the REST
    /// resource — no refunded total, no URLs.
    /// </summary>
    public static GatewayPaymentSnapshot ToSnapshot(PaynetWebhookTransaction transaction, out bool recognised)
    {
        var status = ToStatus(transaction.Status, out recognised);
        var amount = PaynetAmounts.FromDram(transaction.Amount);
        var settled = status is PaymentStatus.Captured or PaymentStatus.Refunded;

        return new GatewayPaymentSnapshot(
            transaction.Uuid ?? string.Empty,
            status,
            AuthorizedAmount: settled ? amount : null,
            CapturedAmount: settled ? amount : null,
            // Paynet's status enum has a single refunded value and a partial refund leaves the
            // payment `completed` with a non-zero refunded_amount. So `refunded` here means the
            // whole amount went back, which is the only reading under which the webhook's missing
            // refunded total is recoverable. A `completed` webhook reports nothing about refunds,
            // so it stays null rather than resetting a partial refund we already recorded.
            RefundedAmount: status == PaymentStatus.Refunded ? amount : null,
            Method: new GatewayPaymentMethod(MethodKind(transaction.Processor), transaction.Processor),
            GatewayStatusCode: transaction.Status,
            FailureCode: status == PaymentStatus.Failed ? transaction.Status : null,
            FailureMessage: status == PaymentStatus.Failed ? "The processor declined the payment." : null);
    }

    public static GatewayRefundSnapshot ToSnapshot(PaynetRefund refund) => new(
        refund.Uuid ?? string.Empty,
        ToRefundStatus(refund.Status),
        PaynetAmounts.FromDram(refund.Amount),
        refund.Status,
        SettledAt: ParseTime(refund.CompletedAt));

    /// <summary>What the customer has to do next: scan, or follow a redirect.</summary>
    private static CustomerAction ToAction(PaynetPayment payment)
    {
        if (string.Equals(payment.PaymentFlow, "qr", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(payment.QrCode))
        {
            var parameters = new Dictionary<string, string> { ["qrCode"] = payment.QrCode };
            if (!string.IsNullOrWhiteSpace(payment.Rrn)) parameters["rrn"] = payment.Rrn;

            return new CustomerAction(CustomerActionKind.Display, Parameters: parameters);
        }

        // `redirect_url` appears with redirect_mode=direct and goes straight to the bank, skipping
        // Paynet's own page. Preferred when present because it is one hop fewer for the customer.
        var url = !string.IsNullOrWhiteSpace(payment.RedirectUrl) ? payment.RedirectUrl : payment.CheckoutUrl;

        return string.IsNullOrWhiteSpace(url)
            ? CustomerAction.None
            : new CustomerAction(CustomerActionKind.Redirect, url, "GET");
    }

    public static DateTime? ParseTime(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
