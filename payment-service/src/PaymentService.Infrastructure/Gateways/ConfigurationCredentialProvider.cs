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

        var section = _configuration.GetSection($"Gateways:{gatewayKey}:Credentials:{credentialRef}");
        if (!section.Exists()) return Task.FromResult<GatewayCredentials?>(null);

        var values = section.GetChildren()
            .Where(child => child.Value is not null)
            .ToDictionary(child => child.Key, child => child.Value!, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<GatewayCredentials?>(
            values.Count == 0 ? null : new GatewayCredentials(values));
    }
}
