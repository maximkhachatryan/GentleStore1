namespace GentleStore.Domain.Entities;

/// <summary>
/// One customer on one browser. Created when an invite is redeemed and referenced by the
/// long-lived cookie that browser then carries, so every future request — and every future
/// order — can be attributed to a known customer. Only the token hash is stored.
/// </summary>
public class CustomerSession
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Invite this session was born from; null once that invite row is pruned.</summary>
    public Guid? CustomerInviteId { get; set; }

    /// <summary>Base64url SHA-256 of the session secret held in the browser cookie.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    /// <summary>Set when the store signs this device out; revoked sessions are never revived.</summary>
    public DateTime? RevokedAt { get; set; }

    public string? CreatedIp { get; set; }
    public string? LastSeenIp { get; set; }
    public string? UserAgent { get; set; }

    public Store? Store { get; set; }
    public Customer? Customer { get; set; }
    public CustomerInvite? Invite { get; set; }
}
