namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>
/// What a gateway can actually do, declared rather than discovered.
/// <para>
/// Gateways differ in ways the caller feels: some cannot partially refund, some have no manual
/// capture, some never push notifications and have to be polled. Declaring it up front lets the
/// orchestrator reject an impossible request with a clear reason before any money is touched,
/// instead of surfacing a gateway's own error and hoping the merchant can read it.
/// </para>
/// </summary>
[Flags]
public enum GatewayCapabilities
{
    None = 0,

    /// <summary>Can authorize now and capture later.</summary>
    ManualCapture = 1 << 0,

    /// <summary>Can capture less than was authorized.</summary>
    PartialCapture = 1 << 1,

    Refunds = 1 << 2,

    /// <summary>Can refund part of a capture, and can do so more than once.</summary>
    PartialRefunds = 1 << 3,

    /// <summary>Can void an authorization before capture.</summary>
    Cancellation = 1 << 4,

    /// <summary>Pushes notifications. A gateway without this must be polled to stay current.</summary>
    Webhooks = 1 << 5,

    /// <summary>Supports reading a payment back, which is what reconciliation needs.</summary>
    StatusPolling = 1 << 6,

    /// <summary>Sends the customer to a page it hosts, then back to <c>ReturnUrl</c>.</summary>
    HostedRedirect = 1 << 7,

    /// <summary>Completed in the browser with a client SDK and a token from us.</summary>
    ClientSideConfirmation = 1 << 8
}
