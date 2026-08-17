namespace PaymentService.Domain.Common;

/// <summary>
/// Just enough ISO 4217 to get the decimal point right. Most currencies use two decimals, so only
/// the exceptions are listed — this is a lookup, not a currency registry, and it deliberately does
/// not try to decide which currencies the platform supports.
/// </summary>
public static class Currencies
{
    private const int DefaultExponent = 2;

    private static readonly HashSet<string> ZeroDecimal = new(StringComparer.Ordinal)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG",
        "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private static readonly HashSet<string> ThreeDecimal = new(StringComparer.Ordinal)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND"
    };

    /// <summary>Number of decimal places, i.e. how many minor units make one major unit.</summary>
    public static int Exponent(string currency)
    {
        var code = Normalize(currency);
        if (ZeroDecimal.Contains(code)) return 0;
        if (ThreeDecimal.Contains(code)) return 3;
        return DefaultExponent;
    }

    /// <summary>10^<see cref="Exponent"/> — 100 for USD, 1 for JPY, 1000 for KWD.</summary>
    public static decimal Scale(string currency) => Exponent(currency) switch
    {
        0 => 1m,
        3 => 1000m,
        _ => 100m
    };

    /// <summary>Three ASCII letters, uppercased. Shape only — no attempt to verify the code exists.</summary>
    public static bool IsWellFormed(string? currency) =>
        currency is { Length: 3 } && currency.All(char.IsAsciiLetter);

    public static string Normalize(string? currency) =>
        IsWellFormed(currency)
            ? currency!.ToUpperInvariant()
            : throw new ArgumentException($"'{currency}' is not a three-letter currency code.", nameof(currency));
}
