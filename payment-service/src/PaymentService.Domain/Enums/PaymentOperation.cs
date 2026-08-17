namespace PaymentService.Domain.Enums;

/// <summary>What a <c>PaymentAttempt</c> was trying to do at the gateway.</summary>
public enum PaymentOperation
{
    Create = 0,
    Capture = 1,
    Cancel = 2,
    Refund = 3,

    /// <summary>A read, used to reconcile when a webhook never arrived.</summary>
    Fetch = 4,

    /// <summary>State applied from an inbound notification rather than a call we made.</summary>
    Webhook = 5
}
