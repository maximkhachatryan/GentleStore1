using System.Globalization;
using System.Text.RegularExpressions;

namespace GentleStore.Api.Common;

/// <summary>Canonicalises the domain names stores bring, so one domain only ever has one spelling.</summary>
public static partial class HostNames
{
    private static readonly IdnMapping Idn = new();

    /// <summary>
    /// Reduces what someone typed or a browser sent ("https://WWW.Flowers.am/shop", "flowers.am:443")
    /// to the bare lowercase ASCII name ("flowers.am"); an Armenian ".հայ" name becomes its "xn--"
    /// form. A leading "www." is dropped: the www name redirects to the bare one. Null for anything
    /// that is not a valid public domain name — a single label, an IP address, an empty string.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var host = input.Trim();
        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) host = host[(scheme + 3)..];

        var end = host.IndexOfAny(['/', '?', '#', ':']);
        if (end >= 0) host = host[..end];
        host = host.TrimEnd('.');

        try
        {
            // Internationalised names (".հայ") are stored and matched in their ASCII form, which is
            // what browsers send in the Host header and what certificates are issued for.
            host = Idn.GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];

        return host.Length <= 253 && DomainName().IsMatch(host) ? host : null;
    }

    /// <summary>The host of a configured origin such as Storefront:Url, normalised the same way.</summary>
    public static string? OfUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.IdnHost.ToLowerInvariant() : null;

    /// <summary>Two or more labels, each 1–63 letters, digits or inner hyphens, ending in a non-numeric TLD.</summary>
    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?:[a-z]{2,63}|xn--[a-z0-9-]{1,59})$")]
    private static partial Regex DomainName();
}
