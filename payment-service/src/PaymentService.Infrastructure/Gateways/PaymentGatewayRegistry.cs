using System.Diagnostics.CodeAnalysis;
using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Infrastructure.Gateways;

/// <summary>
/// Built from whatever integrations DI can see, so registering a gateway is a single
/// <c>AddSingleton&lt;IPaymentGateway, StripeGateway&gt;()</c> and nothing else changes.
/// </summary>
public sealed class PaymentGatewayRegistry : IPaymentGatewayRegistry
{
    private readonly Dictionary<string, IPaymentGateway> _gateways;
    private readonly Dictionary<string, IGatewayWebhookReader> _readers;

    public PaymentGatewayRegistry(IEnumerable<IPaymentGateway> gateways, IEnumerable<IGatewayWebhookReader> readers)
    {
        _gateways = new Dictionary<string, IPaymentGateway>(StringComparer.OrdinalIgnoreCase);
        _readers = new Dictionary<string, IGatewayWebhookReader>(StringComparer.OrdinalIgnoreCase);

        foreach (var gateway in gateways)
        {
            // Fail at startup rather than routing payments to whichever registration happened to
            // win. A duplicate key is always a wiring mistake.
            if (!_gateways.TryAdd(gateway.Key, gateway))
                throw new InvalidOperationException($"Two payment gateways are registered under the key '{gateway.Key}'.");
        }

        foreach (var reader in readers)
        {
            if (!_readers.TryAdd(reader.Key, reader))
                throw new InvalidOperationException($"Two webhook readers are registered under the key '{reader.Key}'.");

            // A reader whose key matches no gateway would silently never be called, and its
            // endpoint would answer as if the gateway did not exist.
            if (!_gateways.ContainsKey(reader.Key))
                throw new InvalidOperationException($"Webhook reader '{reader.Key}' has no matching payment gateway.");
        }
    }

    public IReadOnlyCollection<IPaymentGateway> All => _gateways.Values;

    public bool TryGet(string key, [NotNullWhen(true)] out IPaymentGateway? gateway) =>
        _gateways.TryGetValue(key ?? string.Empty, out gateway);

    public IPaymentGateway Require(string key) =>
        TryGet(key, out var gateway)
            ? gateway
            : throw new InvalidOperationException(
                $"No payment gateway is registered under the key '{key}'. " +
                "A payment references it, so the integration must stay registered as long as those payments exist.");

    public bool TryGetWebhookReader(string key, [NotNullWhen(true)] out IGatewayWebhookReader? reader) =>
        _readers.TryGetValue(key ?? string.Empty, out reader);
}
