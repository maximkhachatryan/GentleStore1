namespace PaymentService.Application.Payments;

/// <summary>
/// Why an operation did not happen. Stable codes rather than prose, following the same convention
/// as GentleStore's <c>PlaceOrderProblem</c>: the caller maps them to its own copy, and a wording
/// change here never breaks a storefront.
/// </summary>
public enum PaymentProblem
{
    None = 0,

    // ---- Request ----
    InvalidAmount,
    InvalidCurrency,
    InvalidReturnUrl,
    MetadataTooLarge,

    // ---- Routing ----
    /// <summary>The merchant has no enabled account with any gateway.</summary>
    NoGatewayConfigured,

    /// <summary>A gateway was named explicitly and nothing is registered under that key.</summary>
    UnknownGateway,

    /// <summary>The merchant's account for that gateway exists but is switched off.</summary>
    GatewayDisabled,

    /// <summary>No configured account accepts this currency.</summary>
    UnsupportedCurrency,

    /// <summary>Credentials could not be resolved. An operator problem, not a customer one.</summary>
    GatewayMisconfigured,

    // ---- State ----
    PaymentNotFound,
    RefundNotFound,

    /// <summary>The operation is not legal from the payment's current status.</summary>
    InvalidState,

    /// <summary>The gateway cannot do what was asked — partial capture, refunds, cancellation.</summary>
    CapabilityNotSupported,

    CaptureExceedsAuthorized,
    RefundExceedsCaptured,

    /// <summary>Same idempotency key, different request body.</summary>
    IdempotencyConflict,

    // ---- Gateway ----
    /// <summary>The issuer said no. Terminal for this payment; a retry means a new one.</summary>
    GatewayDeclined,

    /// <summary>The gateway refused the request itself.</summary>
    GatewayRejected,

    /// <summary>Unreachable or erroring. Retryable — the payment stays in flight.</summary>
    GatewayUnavailable,

    /// <summary>
    /// The gateway was reached and the answer was lost. The payment may or may not exist there,
    /// so it is left in flight for the reconciler rather than being called failed.
    /// </summary>
    GatewayOutcomeUnknown
}
