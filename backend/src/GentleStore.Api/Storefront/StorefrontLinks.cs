namespace GentleStore.Api.Storefront;

public static class StorefrontLinks
{
    /// <summary>
    /// Builds the personal invite link handed to a customer.
    /// <para>
    /// The secret rides in the URL fragment on purpose: fragments are never sent to a server, so
    /// the token stays out of proxy access logs, Referer headers and analytics. The route must
    /// stay in sync with the storefront app (apps/storefront/src/App.tsx).
    /// </para>
    /// </summary>
    public static string Invite(string storefrontUrl, string slug, string token) =>
        $"{storefrontUrl.TrimEnd('/')}/{slug}/welcome#i={token}";
}
