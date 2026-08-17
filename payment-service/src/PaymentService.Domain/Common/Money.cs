namespace PaymentService.Domain.Common;

/// <summary>
/// An amount in the currency's smallest unit — 1050 "USD" is $10.50.
/// <para>
/// Minor units rather than <c>decimal</c> because that is the only representation every gateway
/// agrees on. Gateways that accept a decimal string still settle in integers, and rounding a
/// major-unit amount differently from the gateway is how a payment ends up a cent off a captured
/// total and stops reconciling. GentleStore's own <c>Order.Total</c> stays <c>decimal</c>; the
/// conversion happens once, here, at the boundary.
/// </para>
/// </summary>
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public long MinorUnits { get; }

    /// <summary>Normalized to uppercase. Empty only on <c>default(Money)</c>, which is never valid.</summary>
    public string Currency { get; }

    private Money(long minorUnits, string currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public static Money FromMinorUnits(long minorUnits, string currency) =>
        new(minorUnits, Currencies.Normalize(currency));

    /// <summary>
    /// Rounds half away from zero, matching how prices are quoted to customers rather than
    /// <c>decimal</c>'s banker's-rounding default.
    /// </summary>
    public static Money FromDecimal(decimal amount, string currency)
    {
        var code = Currencies.Normalize(currency);
        var scaled = Math.Round(amount * Currencies.Scale(code), 0, MidpointRounding.AwayFromZero);

        if (scaled is > long.MaxValue or < long.MinValue)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount is out of range for a payment.");

        return new Money((long)scaled, code);
    }

    public static Money Zero(string currency) => new(0, Currencies.Normalize(currency));

    public decimal ToDecimal() => MinorUnits / Currencies.Scale(Currency);

    public bool IsZero => MinorUnits == 0;
    public bool IsPositive => MinorUnits > 0;

    public Money Add(Money other) => new(MinorUnits + Same(other), Currency);
    public Money Subtract(Money other) => new(MinorUnits - Same(other), Currency);

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);

    public static bool operator >(Money a, Money b) => a.MinorUnits > a.Same(b);
    public static bool operator <(Money a, Money b) => a.MinorUnits < a.Same(b);
    public static bool operator >=(Money a, Money b) => a.MinorUnits >= a.Same(b);
    public static bool operator <=(Money a, Money b) => a.MinorUnits <= a.Same(b);
    public static bool operator ==(Money a, Money b) => a.Equals(b);
    public static bool operator !=(Money a, Money b) => !a.Equals(b);

    public int CompareTo(Money other) => MinorUnits.CompareTo(Same(other));

    public bool Equals(Money other) =>
        MinorUnits == other.MinorUnits && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(MinorUnits, Currency);

    public override string ToString() =>
        $"{ToDecimal().ToString($"F{Currencies.Exponent(Currency)}", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    /// <summary>
    /// Mixing currencies is a bug, never a conversion: this service does no FX, and silently
    /// treating 100 EUR as 100 USD would be a real loss.
    /// </summary>
    private long Same(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        return other.MinorUnits;
    }
}
