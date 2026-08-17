namespace PaymentService.Domain.Enums;

/// <summary>Lifecycle of a row in the webhook inbox.</summary>
public enum GatewayEventStatus
{
    /// <summary>Stored, signature valid, not yet applied.</summary>
    Received = 0,

    Processed = 1,

    /// <summary>
    /// Understood but deliberately not applied: a duplicate delivery, or a notification describing
    /// a state the payment has already moved past. Not an error.
    /// </summary>
    Ignored = 2,

    /// <summary>Signature check failed. Stored anyway — a burst of these is an attack worth seeing.</summary>
    InvalidSignature = 3,

    /// <summary>Valid but could not be applied. Retried by the reconciler.</summary>
    Failed = 4
}
