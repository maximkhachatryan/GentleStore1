namespace GentleStore.Domain.Entities;

/// <summary>
/// One line of an order. Names and prices are copied in rather than read through the product
/// references, so editing or deleting a product never rewrites history.
/// </summary>
public class OrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }

    /// <summary>Kept for reporting; null once the product is deleted.</summary>
    public Guid? ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>Flattened variant description, e.g. "Size: 250g, Grind: Filter".</summary>
    public string? VariantLabel { get; set; }

    public int Quantity { get; set; }

    /// <summary>Null for a "price on request" item until the store quotes it.</summary>
    public decimal? UnitPrice { get; set; }

    public int DisplayOrder { get; set; }

    public Order? Order { get; set; }

    /// <summary>Null whenever the unit price is still unknown.</summary>
    public decimal? LineTotal => UnitPrice * Quantity;
}
