using GentleStore.Domain.Enums;

namespace GentleStore.Domain.Entities;

/// <summary>
/// A request to buy, not a reservation: the catalogue tracks availability as a yes/no flag and
/// nothing is held back, so two customers can order the last item. The store confirms what it can
/// actually fulfil.
/// </summary>
public class Order
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    /// <summary>Short per-store code the store and the customer can both say out loud ("BS-1042").</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Always set — a guest checkout creates the customer record it points to.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>The browser that placed it; null once that session row is pruned.</summary>
    public Guid? CustomerSessionId { get; set; }

    /// <summary>How well the store knew this person at the moment of ordering.</summary>
    public CustomerIdentityTier IdentityTier { get; set; }

    public OrderStatus Status { get; set; }

    // Contact details are snapshotted: a customer's phone or name may change later, and the order
    // has to keep saying what was agreed at the time.
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactPhoneNormalized { get; set; } = string.Empty;

    public FulfilmentMethod Fulfilment { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? CustomerNote { get; set; }

    /// <summary>Staff-only note, never shown in the storefront.</summary>
    public string? StoreNote { get; set; }

    /// <summary>Snapshot — a store can change its currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Null while any line is still unpriced ("price on request").</summary>
    public decimal? Total { get; set; }

    public DateTime PlacedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Store? Store { get; set; }
    public Customer? Customer { get; set; }
    public CustomerSession? Session { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
