namespace GentleStore.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>The regular price. Null means "price on request".</summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// What the customer pays while the product is on sale. Ignored unless it is below
    /// <see cref="Price"/> and the sale has not ended — see <see cref="Pricing.SalePricing"/>.
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// When the sale stops, for the product and all its variants. Null means it runs until the
    /// store ends it. Left in place once passed: an ended sale simply stops counting.
    /// </summary>
    public DateTime? SaleEndsAt { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Store Store { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
