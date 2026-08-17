namespace PaymentService.Domain.Enums;

/// <summary>
/// The one status GentleStore is expected to act on. Gateways each have their own vocabulary
/// ("succeeded", "SALE", "AUTHORISED", "settled"); an integration's job is to map its vocabulary
/// onto this set so the merchant never has to learn a gateway's.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Recorded locally; the gateway has not been called yet, or has not answered.</summary>
    Pending = 0,

    /// <summary>The customer has to do something — redirect, 3-D Secure, scan a QR, open an app.</summary>
    RequiresAction = 1,

    /// <summary>
    /// Handed off and out of everyone's hands for a while. Bank transfers and some wallets settle
    /// hours or days later, so this is not a failure — it is a wait.
    /// </summary>
    Processing = 2,

    /// <summary>Funds are held but not taken. Only reachable with <see cref="CaptureMode.Manual"/>.</summary>
    Authorized = 3,

    /// <summary>Money has moved. This is the status that should let an order be fulfilled.</summary>
    Captured = 4,

    PartiallyRefunded = 5,
    Refunded = 6,

    /// <summary>Declined by the issuer or rejected by the gateway. Terminal — a retry is a new payment.</summary>
    Failed = 7,

    /// <summary>Cancelled before capture, by the merchant or the customer.</summary>
    Cancelled = 8,

    /// <summary>The customer never came back and the gateway's window closed.</summary>
    Expired = 9
}
