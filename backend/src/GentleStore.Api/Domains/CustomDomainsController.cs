using GentleStore.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.Domains;

[ApiController]
[AllowAnonymous]
public class CustomDomainsController : ControllerBase
{
    private readonly StoreDomains _domains;

    public CustomDomainsController(StoreDomains domains) => _domains = domains;

    /// <summary>
    /// Asked by Caddy before it requests a certificate for a name it has not served before. Only
    /// stores' own domains pass, so anyone pointing a domain at the server cannot make us request
    /// certificates for it. Not routed from outside: Caddy reaches it over the compose network.
    /// </summary>
    [HttpGet("api/internal/tls/allowed")]
    public async Task<IActionResult> TlsAllowed(string? domain, CancellationToken ct) =>
        await _domains.FindAsync(domain, ct) is null ? NotFound() : Ok();

    /// <summary>Which store a storefront opened on a custom domain is showing.</summary>
    [HttpGet("api/public/hosts/{host}")]
    public async Task<IActionResult> Host(string host, CancellationToken ct)
    {
        var store = await _domains.FindAsync(host, ct);
        return store is null ? NotFound() : Ok(new StoreHostDto(store.Slug));
    }
}
