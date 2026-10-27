using GentleStore.Api.Common;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Domains;

public sealed record DomainStore(Guid Id, string Slug, string Domain);

/// <summary>Finds the store a request's host belongs to, when it came in on a store's own domain.</summary>
public class StoreDomains
{
    private readonly AppDbContext _db;

    public StoreDomains(AppDbContext db) => _db = db;

    /// <summary>
    /// The active store whose domain <paramref name="host"/> is, with or without "www.". Null for
    /// the platform's own hosts and anything unknown, which callers treat as the platform.
    /// </summary>
    public async Task<DomainStore?> FindAsync(string? host, CancellationToken ct)
    {
        var domain = HostNames.Normalize(host);
        if (domain is null) return null;

        return await _db.Stores.AsNoTracking()
            .Where(s => s.CustomDomain == domain && s.IsActive)
            .Select(s => new DomainStore(s.Id, s.Slug, domain))
            .FirstOrDefaultAsync(ct);
    }
}
