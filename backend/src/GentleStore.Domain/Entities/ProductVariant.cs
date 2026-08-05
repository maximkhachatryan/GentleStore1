namespace GentleStore.Domain.Entities;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DisplayOrder { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<VariantAttribute> Attributes { get; set; } = new List<VariantAttribute>();
}
