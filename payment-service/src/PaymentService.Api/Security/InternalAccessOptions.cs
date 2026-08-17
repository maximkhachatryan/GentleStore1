namespace PaymentService.Api.Security;

/// <summary>
/// An optional shared-secret check on the merchant-facing API.
/// <para>
/// The primary control is the network: PaymentService is not published, and only GentleStore can
/// reach it. This is the second lock, and it is off unless <see cref="Keys"/> is populated — in
/// development, two containers on one machine gain nothing from it.
/// </para>
/// <para>
/// Worth turning on in production anyway. "Internal" stops being true faster than anyone expects —
/// a sidecar, a debug ingress, a shared cluster — and an unauthenticated endpoint that can issue
/// refunds is a bad thing to discover has become reachable. Retrofitting it after GentleStore is
/// live in production means coordinating a change across two services.
/// </para>
/// </summary>
public sealed class InternalAccessOptions
{
    public const string SectionName = "InternalAccess";

    public string HeaderName { get; set; } = "X-Internal-Key";

    /// <summary>
    /// Accepted keys. A list rather than one value so a key can be rotated without downtime:
    /// add the new one, roll the callers, drop the old one.
    /// </summary>
    public List<string> Keys { get; set; } = new();

    public bool IsEnabled => Keys.Count > 0;
}
