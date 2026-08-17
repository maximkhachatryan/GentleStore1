using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PaymentService.Api.Contracts;
using PaymentService.Application.Abstractions;

namespace PaymentService.Api.Idempotency;

/// <summary>
/// Applied to the endpoints that move money. Reads <c>Idempotency-Key</c>, claims it before the
/// action runs, and replays the stored response for a repeat of the same request.
/// <para>
/// A checkout POST gets retried — by the caller, by a proxy, or by a customer pressing the button
/// again — and without this each retry is a fresh charge. The key is optional so that reads and
/// scripts are not burdened with one, but any caller creating a payment should send it.
/// </para>
/// </summary>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 128;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyStore _store;

    public IdempotencyFilter(IIdempotencyStore store) => _store = store;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[HeaderName].ToString();

        // No key means the caller accepts that a retry may charge twice. Their choice, and forcing
        // one would break every ad-hoc call against the API.
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            context.Result = new BadRequestObjectResult(new
            {
                code = "invalid_idempotency_key",
                detail = $"{HeaderName} must be at most {MaxKeyLength} characters."
            });
            return;
        }

        // Scoped per merchant, so the store cannot be probed with a guessed key from another
        // tenant. Without a merchant the key has no scope, so it is ignored rather than trusted.
        if (!TryResolveMerchantId(context, out var merchantId))
        {
            await next();
            return;
        }

        var ct = context.HttpContext.RequestAborted;
        var request = context.HttpContext.Request;
        var endpoint = $"{request.Method} {request.Path}";
        var hash = HashArguments(context.ActionArguments);

        var reservation = await _store.ReserveAsync(merchantId, key, endpoint, hash, ct);

        switch (reservation.Outcome)
        {
            case IdempotencyOutcome.Replay:
                context.Result = new ContentResult
                {
                    StatusCode = reservation.ResponseStatusCode,
                    Content = reservation.ResponseBody,
                    ContentType = "application/json"
                };
                return;

            case IdempotencyOutcome.InProgress:
                // Deliberately not a wait. The original request is still running; blocking here
                // would just move the timeout that caused the retry in the first place.
                context.Result = new ConflictObjectResult(new
                {
                    code = "idempotency_in_progress",
                    detail = "A request with this key is still being processed. Retry shortly."
                });
                return;

            case IdempotencyOutcome.Conflict:
                context.Result = new ConflictObjectResult(new
                {
                    code = "idempotency_conflict",
                    detail = "This key was already used with a different request."
                });
                return;
        }

        var executed = await next();

        // An unhandled exception leaves the outcome unknown, but the reservation must not become a
        // permanent 409 for a payment that may never have been created. Released so a genuine
        // retry can run; the payment row and its attempt survive either way.
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await _store.ReleaseAsync(merchantId, key, ct);
            return;
        }

        var (statusCode, body, paymentId) = Describe(executed.Result);

        // 5xx is our fault and inherently retryable, so the key is freed rather than pinning the
        // caller to a failure. 4xx is recorded: repeating an invalid request should keep getting
        // the same answer, not a new attempt at the gateway.
        if (statusCode is null or >= StatusCodes.Status500InternalServerError)
        {
            await _store.ReleaseAsync(merchantId, key, ct);
            return;
        }

        await _store.CompleteAsync(merchantId, key, statusCode.Value, body, paymentId, ct);
    }

    private static (int? StatusCode, string? Body, Guid? PaymentId) Describe(IActionResult? result)
    {
        if (result is ObjectResult objectResult)
        {
            var status = objectResult.StatusCode ?? StatusCodes.Status200OK;
            var body = objectResult.Value is null ? null : JsonSerializer.Serialize(objectResult.Value, Json);

            var paymentId = objectResult.Value switch
            {
                InitiatedPaymentDto initiated => initiated.Payment.Id,
                PaymentDto payment => payment.Id,
                _ => (Guid?)null
            };

            return (status, body, paymentId);
        }

        return result is IStatusCodeActionResult statusResult ? (statusResult.StatusCode, null, null) : (null, null, null);
    }

    /// <summary>
    /// Looks for a <c>merchantId</c> argument, or a <c>MerchantId</c> property on a bound body.
    /// Reflection rather than an interface so a new endpoint gets idempotency by adding the filter,
    /// with no change to its request type.
    /// </summary>
    private static bool TryResolveMerchantId(ActionExecutingContext context, out Guid merchantId)
    {
        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is Guid guid && name.Equals("merchantId", StringComparison.OrdinalIgnoreCase))
            {
                merchantId = guid;
                return merchantId != Guid.Empty;
            }

            if (value is null || value.GetType().IsPrimitive || value is string) continue;

            var property = value.GetType().GetProperty("MerchantId", BindingFlags.Public | BindingFlags.Instance);
            if (property?.GetValue(value) is Guid fromBody && fromBody != Guid.Empty)
            {
                merchantId = fromBody;
                return true;
            }
        }

        merchantId = Guid.Empty;
        return false;
    }

    private static string HashArguments(IDictionary<string, object?> arguments)
    {
        var payload = JsonSerializer.Serialize(
            arguments.Where(a => a.Value is not CancellationToken)
                .OrderBy(a => a.Key, StringComparer.Ordinal)
                .ToDictionary(a => a.Key, a => a.Value),
            Json);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
