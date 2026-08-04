namespace GentleStore.Domain.Entities;

public class ProductTag
{
    public Guid ProductId { get; set; }
    public Guid TagId { get; set; }

    public Product Product { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
