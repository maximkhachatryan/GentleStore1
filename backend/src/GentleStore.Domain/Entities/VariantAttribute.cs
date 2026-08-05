namespace GentleStore.Domain.Entities;

public class VariantAttribute
{
    public Guid Id { get; set; }
    public Guid ProductVariantId { get; set; }

    // Snapshot source references; nullable so definitions/options can be deleted while the snapshot survives.
    public Guid? VariantAttributeDefinitionId { get; set; }
    public Guid? VariantAttributeOptionId { get; set; }

    // Snapshotted values copied at creation time.
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public ProductVariant Variant { get; set; } = null!;
    public VariantAttributeDefinition? Definition { get; set; }
    public VariantAttributeOption? Option { get; set; }
}
