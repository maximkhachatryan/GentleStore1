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

    // ---- Online payment ----

    /// <summary>
    /// Whether this order has to be paid online. Snapshotted at placement rather than read from the
    /// store: a merchant who switches payments off tomorrow must not turn an unpaid order into one
    /// nobody ever owed for, and switching them on must not retroactively block orders already
    /// agreed by phone.
    /// </summary>
    public bool PaymentRequired { get; set; }

    /// <summary>PaymentService's payment id. Null until the customer starts paying.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Short code both sides can quote in a support conversation ("PAY-7QK4M2XT").</summary>
    public string? PaymentReference { get; set; }

    /// <summary>
    /// Last status PaymentService reported, mirrored so the order list can be rendered without a
    /// call per row. PaymentService stays the authority; this is a cache.
    /// </summary>
    public string? PaymentStatus { get; set; }

    /// <summary>Set once, when the money is confirmed captured. Never cleared.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Where to send the customer to finish paying. Kept so an abandoned checkout can be resumed:
    /// re-creating the payment would either return no redirect (the idempotency key already has
    /// one) or charge the customer twice (a fresh key).
    /// </summary>
    public string? PaymentCheckoutUrl { get; set; }

    /// <summary>
    /// How many payments have been started for this order. Only ever incremented when the previous
    /// one is dead — declined, cancelled or expired — and it is what makes each attempt's
    /// idempotency key distinct without letting a reload become a second charge.
    /// </summary>
    public int PaymentAttempt { get; set; }

    public DateTime PlacedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Store? Store { get; set; }
    public Customer? Customer { get; set; }
    public CustomerSession? Session { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
