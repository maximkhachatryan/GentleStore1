using PaymentService.Domain.Common;
using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Its own record rather than a field on the payment, because a payment can be refunded in several
/// pieces, each settling at its own pace and each able to fail on its own.
/// </summary>
public class Refund
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;
    public long AmountMinor { get; set; }

    public RefundStatus Status { get; set; }

    /// <summary>Merchant's reason, free text. Gateways that have a reason enum map it themselves.</summary>
    public string? Reason { get; set; }

    public string? GatewayRefundId { get; set; }
    public string? GatewayStatusCode { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>When the gateway confirmed the money was on its way back.</summary>
    public DateTime? SettledAt { get; set; }

    public uint Version { get; set; }

    public Payment? Payment { get; set; }

    public Money Amount => Money.FromMinorUnits(AmountMinor, Currency);
}
