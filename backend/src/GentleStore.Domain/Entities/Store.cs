using GentleStore.Domain.Enums;

namespace GentleStore.Domain.Entities;

public class Store
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    /// <summary>Whether the storefront is open to everyone or only to invited customers.</summary>
    public StorefrontAccessMode StorefrontAccess { get; set; } = StorefrontAccessMode.Public;

    /// <summary>
    /// Whether customers pay online before the store works the order. Switched on per tenant by a
    /// super admin, who also points the store at its own account with the payment provider.
    /// <para>
    /// Only the switch lives here. Which providers are enabled, and with which credentials, is held
    /// by PaymentService against this store id — duplicating it would give two answers to the same
    /// question, and the one that matters is the one PaymentService will actually charge on.
    /// </para>
    /// </summary>
    public bool PaymentsEnabled { get; set; }

    /// <summary>
    /// Counter behind the human-readable order numbers. Incremented by an atomic UPDATE so two
    /// simultaneous checkouts cannot be handed the same number.
    /// </summary>
    public int OrderSequence { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<VariantAttributeDefinition> VariantAttributeDefinitions { get; set; }
        = new List<VariantAttributeDefinition>();
}
