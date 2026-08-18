using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GentleStore.Infrastructure.Payments;

/// <summary>
/// Checks that a status callback really came from PaymentService.
/// <para>
/// The callback endpoint is a plain POST that says "this order is paid", so whatever can reach it
/// can mark orders paid. On the shipped deployment only the compose network can, but that is a
/// property of the deployment rather than of the code — the signature is what makes the endpoint
/// safe on its own terms.
/// </para>
/// </summary>
public interface IPaymentCallbackVerifier
{
    /// <summary>True when no secret is configured, so a private-network deployment still works unsigned.</summary>
    bool IsOptional { get; }

    bool Verify(string rawBody, string? presentedSignature);
}

public sealed class PaymentCallbackVerifier : IPaymentCallbackVerifier
{
    public const string HeaderName = "X-Payment-Signature";
    private const string Prefix = "sha256=";

    private readonly PaymentServiceOptions _options;

    public PaymentCallbackVerifier(IOptions<PaymentServiceOptions> options) => _options = options.Value;

    public bool IsOptional => string.IsNullOrWhiteSpace(_options.CallbackSecret);

    public bool Verify(string rawBody, string? presentedSignature)
    {
        if (IsOptional) return true;
        if (string.IsNullOrWhiteSpace(presentedSignature)) return false;

        var value = presentedSignature.Trim();
        if (value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) value = value[Prefix.Length..];

        byte[] presented;
        try
        {
            presented = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return false;
        }

        if (presented.Length != SHA256.HashSizeInBytes) return false;

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_options.CallbackSecret!),
            Encoding.UTF8.GetBytes(rawBody));

        // Fixed-time: a short-circuiting comparison leaks the expected signature a byte at a time
        // to anyone able to measure the response.
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
