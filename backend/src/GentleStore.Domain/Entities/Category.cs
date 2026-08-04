namespace GentleStore.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
    
}
