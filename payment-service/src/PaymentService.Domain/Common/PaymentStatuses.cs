using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Common;

/// <summary>
/// Which moves are legal, in one place.
/// <para>
/// Needed because notifications arrive out of order. Gateways retry webhooks with backoff, so the
/// "authorized" delivery can easily land after the "captured" one; without a rule saying captured
/// never goes back to authorized, a late retry would quietly un-pay an order that has already
/// shipped.
/// </para>
/// </summary>
public static class PaymentStatuses
{
    /// <summary>Nothing further will happen on its own. A terminal payment is safe to report as final.</summary>
    public static bool IsTerminal(this PaymentStatus status) => status is
        PaymentStatus.Captured or
        PaymentStatus.PartiallyRefunded or
        PaymentStatus.Refunded or
        PaymentStatus.Failed or
        PaymentStatus.Cancelled or
        PaymentStatus.Expired;

    /// <summary>The payment has money attached to it, whether held or taken.</summary>
    public static bool IsSettledOrHeld(this PaymentStatus status) => status is
        PaymentStatus.Authorized or
        PaymentStatus.Captured or
        PaymentStatus.PartiallyRefunded or
        PaymentStatus.Refunded;

    /// <summary>Waiting on the customer or the gateway; a reconciler should keep checking.</summary>
    public static bool IsInFlight(this PaymentStatus status) => status is
        PaymentStatus.Pending or
        PaymentStatus.RequiresAction or
        PaymentStatus.Processing;

    public static bool CanCapture(this PaymentStatus status) => status is PaymentStatus.Authorized;

    public static bool CanCancel(this PaymentStatus status) => status is
        PaymentStatus.Pending or
        PaymentStatus.RequiresAction or
        PaymentStatus.Processing or
        PaymentStatus.Authorized;

    public static bool CanRefund(this PaymentStatus status) => status is
        PaymentStatus.Captured or
        PaymentStatus.PartiallyRefunded;

    /// <summary>
    /// Whether an observed status may replace the current one. Progress is allowed, regression is
    /// not, and re-observing the same status is allowed so a redelivery can refresh amounts.
    /// </summary>
    public static bool CanTransitionTo(this PaymentStatus current, PaymentStatus next)
    {
        if (current == next) return true;

        return current switch
        {
            PaymentStatus.Pending => next is not PaymentStatus.Pending,

            PaymentStatus.RequiresAction => next is not PaymentStatus.Pending,

            PaymentStatus.Processing => next is not (PaymentStatus.Pending or PaymentStatus.RequiresAction),

            PaymentStatus.Authorized => next is
                PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded or
                PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired,

            // Refunds are the only way out of a completed payment. Failed/Cancelled are excluded on
            // purpose: once the money moved, a gateway saying "failed" is a dispute or a reversal,
            // both of which need a human, not an automatic status flip.
            PaymentStatus.Captured => next is
                PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded,

            PaymentStatus.PartiallyRefunded => next is PaymentStatus.Refunded,

            PaymentStatus.Refunded or
            PaymentStatus.Failed or
            PaymentStatus.Cancelled or
            PaymentStatus.Expired => false,

            _ => false
        };
    }

    /// <summary>
    /// The status implied by how much has been given back. Derived rather than sent by the gateway,
    /// because partial-refund vocabulary is where gateways differ most.
    /// </summary>
    public static PaymentStatus AfterRefund(long capturedMinor, long refundedMinor) =>
        refundedMinor <= 0 ? PaymentStatus.Captured
        : refundedMinor >= capturedMinor ? PaymentStatus.Refunded
        : PaymentStatus.PartiallyRefunded;
}
