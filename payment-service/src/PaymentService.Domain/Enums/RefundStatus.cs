namespace PaymentService.Domain.Enums;

public enum RefundStatus
{
    /// <summary>Recorded locally, not yet accepted by the gateway.</summary>
    Pending = 0,

    /// <summary>Accepted by the gateway but not yet settled back to the customer.</summary>
    Processing = 1,

    Succeeded = 2,
    Failed = 3,

    /// <summary>
    /// Accepted and then reversed by the gateway — rare, but it happens with bank rejections, and a
    /// refund that silently stays <see cref="Succeeded"/> would leave the books wrong.
    /// </summary>
    Cancelled = 4
}
