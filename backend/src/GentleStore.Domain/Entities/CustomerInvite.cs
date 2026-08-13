namespace GentleStore.Domain.Entities;

/// <summary>
/// A single-use invite link handed to one customer. Only the SHA-256 hash of the secret is
/// stored, so a database leak cannot be replayed as a working link; the plaintext exists once,
/// in the response that created the invite.
/// </summary>
public class CustomerInvite
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Base64url SHA-256 of the invite secret. Unique across all stores.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment a browser claims the link; a second claim from elsewhere is refused.</summary>
    public DateTime? RedeemedAt { get; set; }

    /// <summary>Set when the store regenerates or cancels the link before it was used.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Kept for the store's audit trail of who opened the link.</summary>
    public string? RedeemedIp { get; set; }
    public string? RedeemedUserAgent { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Store? Store { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Sessions created by redeeming this invite — at most one under normal use.</summary>
    public ICollection<CustomerSession> Sessions { get; set; } = new List<CustomerSession>();
}
