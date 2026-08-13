namespace GentleStore.Api.Common;

/// <summary>
/// Canonicalises customer phone numbers to bare international digits — the form wa.me links
/// need and the form store-scoped uniqueness is checked against.
/// </summary>
public static class PhoneNumbers
{
    /// <summary>E.164 allows at most 15 digits; a country code plus subscriber number is never shorter than 8.</summary>
    private const int MinDigits = 8;
    private const int MaxDigits = 15;

    public enum Problem
    {
        None,
        Empty,
        /// <summary>Looks like a local number (leading zero, or too short to carry a country code).</summary>
        MissingCountryCode,
        TooLong
    }

    /// <summary>
    /// Reduces free-form input ("+374 (99) 12-34-56", "00374 99 123456") to digits only
    /// ("3749912 3456" → "37499123456").
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized, out Problem problem)
    {
        normalized = string.Empty;

        var digits = new string((input ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            problem = Problem.Empty;
            return false;
        }

        // "00" is the international dialling prefix in most of the world — the E.164 "+".
        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];

        // A trunk-prefixed local number ("099…") is ambiguous: we cannot guess the country.
        if (digits.StartsWith('0') || digits.Length < MinDigits)
        {
            problem = Problem.MissingCountryCode;
            return false;
        }

        if (digits.Length > MaxDigits)
        {
            problem = Problem.TooLong;
            return false;
        }

        normalized = digits;
        problem = Problem.None;
        return true;
    }

    /// <summary>Keeps only the last four digits, for screens a stranger might be looking at.</summary>
    public static string Mask(string normalized)
    {
        if (normalized.Length <= 4) return normalized;
        return $"•••• {normalized[^4..]}";
    }
}
