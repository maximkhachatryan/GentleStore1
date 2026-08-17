using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PaymentService.Api.Security;

public sealed class InternalAccessMiddleware
{
    /// <summary>
    /// Paths the check never applies to. Webhooks come from the public internet and carry a
    /// gateway signature instead — requiring an internal key there would simply break them.
    /// </summary>
    private static readonly string[] Exempt =
    [
        "/health",
        "/api/webhooks",
        "/api/sandbox",
        "/swagger"
    ];

    private readonly RequestDelegate _next;
    private readonly InternalAccessOptions _options;

    public InternalAccessMiddleware(RequestDelegate next, IOptions<InternalAccessOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.IsEnabled || IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var presented = context.Request.Headers[_options.HeaderName].ToString();

        if (!IsAccepted(presented))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "internal_key_required" });
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(PathString path) =>
        Exempt.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Fixed-time comparison across every configured key. A short-circuiting <c>==</c> leaks the
    /// expected value one byte at a time to anyone able to measure the response.
    /// </summary>
    private bool IsAccepted(string presented)
    {
        if (string.IsNullOrEmpty(presented)) return false;

        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        var accepted = false;

        foreach (var key in _options.Keys)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (CryptographicOperations.FixedTimeEquals(presentedBytes, keyBytes)) accepted = true;
        }

        return accepted;
    }
}
