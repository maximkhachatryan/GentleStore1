namespace GentleStore.Domain.Pricing;

/// <summary>What a customer pays right now, and what it would cost without the sale.</summary>
/// <param name="Price">The price to charge. Null means "price on request".</param>
/// <param name="RegularPrice">Set only while a sale is running: the price being discounted.</param>
public readonly record struct EffectivePrice(decimal? Price, decimal? RegularPrice)
{
    public bool OnSale => RegularPrice is not null;
}

/// <summary>
/// The one rule for whether a sale price applies. Everything that shows or charges a price goes
/// through here, so the storefront, checkout and link previews cannot disagree.
/// </summary>
public static class SalePricing
{
    public const int MinPercent = 1;
    public const int MaxPercent = 95;

    /// <summary>
    /// A sale counts only while it is actually cheaper and has not ended. A regular price raised
    /// above the sale price later is fine; one lowered beneath it silently ends the sale rather
    /// than charging more than the regular price.
    /// </summary>
    public static bool IsActive(decimal? price, decimal? salePrice, DateTime? saleEndsAt, DateTime now) =>
        price is not null
        && salePrice is not null
        && salePrice < price
        && salePrice >= 0
        && (saleEndsAt is null || saleEndsAt > now);

    public static EffectivePrice Resolve(decimal? price, decimal? salePrice, DateTime? saleEndsAt, DateTime now) =>
        IsActive(price, salePrice, saleEndsAt, now)
            ? new EffectivePrice(salePrice, price)
            : new EffectivePrice(price, null);

    /// <summary>
    /// Takes <paramref name="percent"/> off <paramref name="price"/>. A price set in whole units
    /// (9 000 AMD) stays in whole units; one with cents keeps two decimals. Returns null when the
    /// rounding leaves nothing off, so a tiny price is not shown as "on sale" for the same amount.
    /// </summary>
    public static decimal? Discount(decimal price, int percent)
    {
        if (price <= 0) return null;
        var raw = price * (100 - percent) / 100m;
        var decimals = price == decimal.Truncate(price) ? 0 : 2;
        var rounded = Math.Round(raw, decimals, MidpointRounding.AwayFromZero);
        return rounded < price ? rounded : null;
    }
}
