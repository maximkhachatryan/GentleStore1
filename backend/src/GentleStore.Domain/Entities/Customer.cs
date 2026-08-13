namespace GentleStore.Domain.Entities;

/// <summary>
/// A person a store invited to its storefront, identified by phone number. Customers never
/// pick a password — access is granted by redeeming a personal invite link, which binds the
/// customer to the browser that opened it (see <see cref="CustomerSession"/>).
/// </summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    /// <summary>Phone as the store typed it, kept for display (e.g. "+374 99 12 34 56").</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Digits only, no leading "+" — the canonical form used for uniqueness and wa.me links.</summary>
    public string PhoneNormalized { get; set; } = string.Empty;

    public string? FullName { get; set; }

    /// <summary>Free-form staff note ("Anna from the flower shop next door").</summary>
    public string? Note { get; set; }

    /// <summary>Blocked customers keep their sessions but are refused at the gate.</summary>
    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When this customer first redeemed an invite. Null while never activated.</summary>
    public DateTime? FirstActivatedAt { get; set; }

    /// <summary>Staff member who added the customer; null once that user is deleted.</summary>
    public Guid? CreatedByUserId { get; set; }

    public Store? Store { get; set; }
    public ICollection<CustomerInvite> Invites { get; set; } = new List<CustomerInvite>();
    public ICollection<CustomerSession> Sessions { get; set; } = new List<CustomerSession>();
}
