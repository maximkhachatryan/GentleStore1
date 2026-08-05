namespace GentleStore.Domain.Entities;

public class VariantAttributeOption
{
    public Guid Id { get; set; }
    public Guid VariantAttributeDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public VariantAttributeDefinition Definition { get; set; } = null!;
}
