using System.Security.Cryptography;
using System.Text;

namespace GentleStore.Api.Storefront;

/// <summary>
/// Secrets for invite links and session cookies. Both are 256-bit random values that only ever
/// exist in plaintext in the browser (and, once, in the response that minted the invite); the
/// database keeps nothing but their hashes.
/// </summary>
public static class StorefrontTokens
{
    private const int SecretBytes = 32;

    public static string NewSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretBytes);
        return ToBase64Url(bytes);
    }

    /// <summary>
    /// Plain SHA-256 — no salt or work factor needed: these are full-entropy random secrets,
    /// not guessable passwords, so there is nothing for an offline attack to shortcut.
    /// </summary>
    public static string Hash(string secret) =>
        ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>Rejects obviously malformed input before it reaches the database.</summary>
    public static bool LooksLikeSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 20 and <= 200
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
