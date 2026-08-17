namespace PaymentService.Application.Abstractions.Gateways;

/// <summary>
/// The secrets one merchant needs to talk to one gateway, resolved at call time and never
/// persisted alongside the account row.
/// <para>
/// A bag of strings rather than a typed class per gateway, because the shape genuinely differs —
/// an API key here, a key plus merchant account plus HMAC there — and the alternative is either a
/// class the abstraction has to know about or a lowest-common-denominator that fits nobody.
/// Integrations name what they need through <see cref="Require"/> and fail loudly when it is
/// missing, which is a configuration error worth crashing a request over.
/// </para>
/// </summary>
public sealed class GatewayCredentials
{
    public static readonly GatewayCredentials Empty = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _values;

    public GatewayCredentials(IReadOnlyDictionary<string, string> values) =>
        _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

    public string? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public bool Has(string name) => !string.IsNullOrWhiteSpace(Get(name));

    public string Require(string name) =>
        Get(name) is { Length: > 0 } value
            ? value
            : throw new GatewayConfigurationException($"Credential '{name}' is missing.");

    public IEnumerable<string> Names => _values.Keys;

    /// <summary>Deliberately reveals nothing — this type ends up in log scopes and exception messages.</summary>
    public override string ToString() => $"GatewayCredentials({_values.Count} value(s))";
}

/// <summary>
/// Thrown when a gateway is configured wrongly rather than behaving badly. Distinct from a gateway
/// failure because no retry will help and the fix is an operator's, not the customer's.
/// </summary>
public sealed class GatewayConfigurationException : Exception
{
    public GatewayConfigurationException(string message) : base(message)
    {
    }
}
