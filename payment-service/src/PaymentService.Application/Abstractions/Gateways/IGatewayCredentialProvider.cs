namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>
/// Turns the opaque <c>CredentialRef</c> on a merchant's account row into the actual secrets.
/// <para>
/// An interface rather than a configuration read so the storage decision stays reversible. The
/// shipped implementation reads configuration, which is fine for a handful of merchants; a
/// platform with many will want a secret manager, and that is a new class here rather than a
/// change to every gateway.
/// </para>
/// </summary>
public interface IGatewayCredentialProvider
{
    /// <summary>Null when the reference resolves to nothing — a misconfiguration, not a decline.</summary>
    Task<GatewayCredentials?> GetAsync(string gatewayKey, string credentialRef, CancellationToken ct = default);
}
