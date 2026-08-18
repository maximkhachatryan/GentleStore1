using Microsoft.Extensions.Configuration;
using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Infrastructure.Gateways;

/// <summary>
/// Resolves credentials from configuration:
/// <code>
/// Gateways:{gatewayKey}:Credentials:{credentialRef}:{name}
/// </code>
/// which in a container is <c>Gateways__stripe__Credentials__acme-live__SecretKey</c>.
/// <para>
/// Configuration is the right floor, not the ceiling: it keeps secrets out of the database and out
/// of the image, and it composes with whatever the deployment already injects — Docker secrets,
/// Kubernetes secrets, a Key Vault configuration provider. A platform holding live keys for many
/// merchants should replace this with a real secret manager, which is one class rather than a
/// change to any gateway.
/// </para>
/// </summary>
public sealed class ConfigurationCredentialProvider : IGatewayCredentialProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationCredentialProvider(IConfiguration configuration) => _configuration = configuration;

    public Task<GatewayCredentials?> GetAsync(string gatewayKey, string credentialRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayKey) || string.IsNullOrWhiteSpace(credentialRef))
            return Task.FromResult<GatewayCredentials?>(null);

        foreach (var key in Lookup(gatewayKey))
        {
            var section = _configuration.GetSection($"Gateways:{key}:Credentials:{credentialRef}");
            if (!section.Exists()) continue;

            var values = section.GetChildren()
                .Where(child => child.Value is not null)
                .ToDictionary(child => child.Key, child => child.Value!, StringComparer.OrdinalIgnoreCase);

            if (values.Count > 0) return Task.FromResult<GatewayCredentials?>(new GatewayCredentials(values));
        }

        return Task.FromResult<GatewayCredentials?>(null);
    }

    /// <summary>
    /// The exact gateway key first, then the provider family before the first hyphen.
    /// <para>
    /// One provider can register several gateway keys when it fronts several payment methods —
    /// Paynet is <c>paynet-arca</c>, <c>paynet-idram</c>, <c>paynet-telcell</c> — but the merchant
    /// holds a single set of credentials for all of them. The fallback lets those live once under
    /// <c>Gateways:paynet:Credentials</c> instead of being copied per method, which is both
    /// tedious and a rotation hazard. A gateway that wants per-method credentials can still
    /// override them under its own key.
    /// </para>
    /// </summary>
    private static IEnumerable<string> Lookup(string gatewayKey)
    {
        yield return gatewayKey;

        var separator = gatewayKey.IndexOf('-');
        if (separator > 0) yield return gatewayKey[..separator];
    }
}
