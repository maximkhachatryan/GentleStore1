using PaymentService.Domain.Common;

namespace PaymentService.Infrastructure.Gateways.Paynet;

/// <summary>
/// Dram conversion, in one place.
/// <para>
/// ISO 4217 gives AMD two decimals (the luma), so <see cref="Money"/> stores 5 000 ֏ as 500 000
/// minor units. Paynet takes <em>whole drams</em> — "amount in dram (integer, minimum 1)". The two
/// therefore differ by a factor of 100, and this is where that is reconciled.
/// </para>
/// <para>
/// Kept in the adapter rather than by declaring AMD zero-decimal in <see cref="Currencies"/>:
/// that class is shared by every integration, and quietly disagreeing with ISO 4217 there would
/// be a trap for the next one. A provider's quirk belongs to the provider.
/// </para>
/// </summary>
internal static class PaynetAmounts
{
    public const string Currency = "AMD";

    /// <summary>
    /// False when the amount is not a whole number of drams. Paynet cannot represent a fraction of
    /// a dram, and rounding on the merchant's behalf would mean charging a different amount than
    /// the order says — so it is refused instead.
    /// </summary>
    public static bool TryToDram(Money money, out long dram)
    {
        dram = 0;
        if (!string.Equals(money.Currency, Currency, StringComparison.Ordinal)) return false;

        var scale = (long)Currencies.Scale(Currency);
        if (money.MinorUnits % scale != 0) return false;

        dram = money.MinorUnits / scale;
        return true;
    }

    public static Money FromDram(long dram) =>
        Money.FromMinorUnits(dram * (long)Currencies.Scale(Currency), Currency);
}
