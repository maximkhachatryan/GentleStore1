using System.Diagnostics.CodeAnalysis;

namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>
/// Resolves a gateway from the key stored on a payment.
/// <para>
/// Lookup by key rather than DI by type because the choice is data: it comes from the merchant's
/// account row, and a payment created last month must still resolve the integration that made it
/// even after the merchant switched providers.
/// </para>
/// </summary>
public interface IPaymentGatewayRegistry
{
    /// <summary>Every registered integration, for the capability listing the console renders.</summary>
    IReadOnlyCollection<IPaymentGateway> All { get; }

    bool TryGet(string key, [NotNullWhen(true)] out IPaymentGateway? gateway);

    /// <summary>Throws when the key is unknown — use for a key that came from our own database.</summary>
    IPaymentGateway Require(string key);

    /// <summary>False for gateways that are polled rather than pushed.</summary>
    bool TryGetWebhookReader(string key, [NotNullWhen(true)] out IGatewayWebhookReader? reader);
}
