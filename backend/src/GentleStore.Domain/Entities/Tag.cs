namespace GentleStore.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}
