using System.Security.Cryptography;
using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure;

/// <summary>
/// Crockford's base32 alphabet: no I, L, O or U, so a reference read down a phone line or copied
/// off a receipt cannot be mistyped as a different valid one, and no accidental words appear.
/// </summary>
public sealed class ReferenceGenerator : IReferenceGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 10;

    public string NewPaymentReference() => $"PAY-{Random()}";

    public string NewRefundReference() => $"REF-{Random()}";

    /// <summary>
    /// Derived, not random. A retry after a timeout has to present the same key as the call that
    /// may already have gone through, and the entity id plus the operation name is the only thing
    /// stable across both attempts.
    /// </summary>
    public string GatewayIdempotencyKey(Guid entityId, string operation) =>
        $"{entityId:N}-{operation.ToLowerInvariant()}";

    private static string Random()
    {
        // ~50 bits of entropy. References travel in redirect URLs, so they have to be unguessable
        // as well as unique.
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);

        return string.Create(Length, bytes.ToArray(), static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = Alphabet[source[i] % Alphabet.Length];
        });
    }
}
