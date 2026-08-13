namespace GentleStore.Api.Storefront;

public class StorefrontOptions
{
    public const string SectionName = "Storefront";

    /// <summary>Public origin of the storefront app, used to build invite links.</summary>
    public string Url { get; set; } = "http://localhost:5174";

    /// <summary>How long an unredeemed invite link stays usable.</summary>
    public int InviteExpiryDays { get; set; } = 14;

    public StorefrontCookieOptions SessionCookie { get; set; } = new();
}

public class StorefrontCookieOptions
{
    /// <summary>The store id is appended, so a customer can hold sessions for several stores at once.</summary>
    public string NamePrefix { get; set; } = "gs_sf";

    /// <summary>
    /// "Lax" is correct whenever the API and the storefront share a registrable domain
    /// (api.example.com + example.com), which is how the shipped deployment is laid out.
    /// Use "None" — which forces Secure — only if they end up on unrelated domains.
    /// </summary>
    public string SameSite { get; set; } = "Lax";

    /// <summary>Leave empty to scope the cookie to the API host only.</summary>
    public string? Domain { get; set; }

    /// <summary>Null follows the request scheme, so local http development still works.</summary>
    public bool? Secure { get; set; }

    /// <summary>
    /// Browsers cap persistent cookies at ~400 days, so "never expires" is implemented as the
    /// maximum window plus a refresh on every visit — an active customer never gets logged out.
    /// </summary>
    public int MaxAgeDays { get; set; } = 400;
}
