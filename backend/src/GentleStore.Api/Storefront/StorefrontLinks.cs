using GentleStore.Domain.Entities;

namespace GentleStore.Api.Storefront;

public static class StorefrontLinks
{
    /// <summary>
    /// Where a store's storefront lives; every page of it is a path below this. That is the root
    /// of the store's own domain when it has one, else "/{slug}" on the platform's. The routes
    /// must stay in sync with the storefront app (apps/storefront/src/App.tsx).
    /// </summary>
    public static string StoreBase(string storefrontUrl, string slug, string? customDomain) =>
        customDomain is null ? $"{storefrontUrl.TrimEnd('/')}/{slug}" : $"https://{customDomain}";

    public static string StoreBase(string storefrontUrl, Store store) =>
        StoreBase(storefrontUrl, store.Slug, store.CustomDomain);

    /// <summary>The origin the store's pages are served from — its own domain, or the platform's.</summary>
    public static string Origin(string storefrontUrl, string? customDomain) =>
        customDomain is null ? storefrontUrl.TrimEnd('/') : $"https://{customDomain}";

    /// <summary>
    /// Builds the personal invite link handed to a customer.
    /// <para>
    /// The secret rides in the URL fragment on purpose: fragments are never sent to a server, so
    /// the token stays out of proxy access logs, Referer headers and analytics.
    /// </para>
    /// </summary>
    public static string Invite(string storefrontUrl, Store store, string token) =>
        $"{StoreBase(storefrontUrl, store)}/welcome#i={token}";
}
