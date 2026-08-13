namespace GentleStore.Domain.Enums;

/// <summary>
/// How well the store knows the person behind an order. Recorded per order, because the same
/// customer can be reached through different doors over time.
/// </summary>
public enum CustomerIdentityTier
{
    /// <summary>
    /// The browser holds a session created by redeeming a store-issued invite. The store sent
    /// that link to a specific WhatsApp number and only its owner could have received it, so the
    /// phone number is verified out-of-band.
    /// </summary>
    Invited = 0,

    /// <summary>Self-declared, but this browser has ordered here before under the same identity.</summary>
    Returning = 1,

    /// <summary>Self-declared, first order from this browser. Nothing is verified yet.</summary>
    Guest = 2
}
