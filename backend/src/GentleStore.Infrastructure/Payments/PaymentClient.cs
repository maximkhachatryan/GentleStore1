using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GentleStore.Infrastructure.Payments;

public sealed class PaymentClient : IPaymentClient
{
    public const string HttpClientName = "payment-service";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _clients;
    private readonly PaymentServiceOptions _options;
    private readonly ILogger<PaymentClient> _log;

    public PaymentClient(
        IHttpClientFactory clients, IOptions<PaymentServiceOptions> options, ILogger<PaymentClient> log)
    {
        _clients = clients;
        _options = options.Value;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;

    // ---- Admin ----

    public Task<PaymentClientResult<IReadOnlyList<PaymentGatewayDto>>> ListGatewaysAsync(CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<PaymentGatewayDto>>(HttpMethod.Get, "api/gateways", null, null, ct);

    public Task<PaymentClientResult<IReadOnlyList<GatewayAccountDto>>> ListAccountsAsync(
        Guid storeId, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<GatewayAccountDto>>(
            HttpMethod.Get, $"api/merchants/{storeId}/gateway-accounts", null, null, ct);

    public Task<PaymentClientResult<GatewayAccountDto>> UpsertAccountAsync(
        Guid storeId, UpsertGatewayAccountRequest request, CancellationToken ct = default) =>
        SendAsync<GatewayAccountDto>(
            HttpMethod.Put, $"api/merchants/{storeId}/gateway-accounts", request, null, ct);

    public Task<PaymentClientResult<GatewayAccountDto>> DisableAccountAsync(
        Guid storeId, Guid accountId, CancellationToken ct = default) =>
        SendAsync<GatewayAccountDto>(
            HttpMethod.Delete, $"api/merchants/{storeId}/gateway-accounts/{accountId}", null, null, ct);

    // ---- Checkout ----

    public Task<PaymentClientResult<InitiatedPaymentDto>> CreatePaymentAsync(
        CreatePaymentRequest request, string idempotencyKey, CancellationToken ct = default) =>
        SendAsync<InitiatedPaymentDto>(HttpMethod.Post, "api/payments", request, idempotencyKey, ct);

    public Task<PaymentClientResult<PaymentDto>> GetPaymentAsync(
        Guid storeId, Guid paymentId, CancellationToken ct = default) =>
        SendAsync<PaymentDto>(HttpMethod.Get, $"api/payments/{paymentId}?merchantId={storeId}", null, null, ct);

    public Task<PaymentClientResult<PaymentDto>> SyncPaymentAsync(
        Guid storeId, Guid paymentId, CancellationToken ct = default) =>
        SendAsync<PaymentDto>(HttpMethod.Post, $"api/payments/{paymentId}/sync?merchantId={storeId}", null, null, ct);

    // ---- Transport ----

    private async Task<PaymentClientResult<T>> SendAsync<T>(
        HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken ct)
        where T : class
    {
        if (!_options.IsConfigured)
            return PaymentClientResult<T>.Fail(PaymentClientProblem.NotConfigured, "payments_not_configured");

        using var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(_options.InternalKey))
            request.Headers.TryAddWithoutValidation("X-Internal-Key", _options.InternalKey);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        try
        {
            var client = _clients.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return value is null
                    ? PaymentClientResult<T>.Fail(PaymentClientProblem.Unavailable, "empty_response")
                    : PaymentClientResult<T>.Ok(value);
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            var (code, detail) = ReadError(payload);

            _log.LogWarning(
                "PaymentService {Method} {Path} answered {Status} ({Code}): {Detail}",
                method, path, (int)response.StatusCode, code, detail);

            return PaymentClientResult<T>.Fail(Classify(response.StatusCode), code, detail);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("PaymentService {Method} {Path} timed out.", method, path);
            return PaymentClientResult<T>.Fail(PaymentClientProblem.Unavailable, "timeout");
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "PaymentService {Method} {Path} is unreachable.", method, path);
            return PaymentClientResult<T>.Fail(PaymentClientProblem.Unavailable, "unreachable", ex.Message);
        }
    }

    /// <summary>
    /// PaymentService's status codes carry the category, which is the whole reason it distinguishes
    /// them: 402 is the customer's card, 422 is the merchant's setup, 5xx is ours.
    /// </summary>
    private static PaymentClientProblem Classify(HttpStatusCode status) => status switch
    {
        HttpStatusCode.PaymentRequired => PaymentClientProblem.Declined,
        HttpStatusCode.UnprocessableContent => PaymentClientProblem.MerchantNotConfigured,
        HttpStatusCode.NotFound => PaymentClientProblem.NotFound,
        HttpStatusCode.BadRequest or HttpStatusCode.Conflict => PaymentClientProblem.Rejected,

        // 502 is the gateway refusing the call — an answer, not a failure to get one. Lumping it in
        // with "unreachable" is worse than pedantry: the caller waits and retries forever instead of
        // concluding the payment is dead and starting a new one. Only 503 and 504 mean we could not
        // reach the gateway, or reached it and lost the answer.
        HttpStatusCode.BadGateway => PaymentClientProblem.Rejected,

        // 504 is "the gateway was reached and the answer was lost" — the payment may well exist, so
        // it must not be reported as a failure. Treated as retryable; a re-read settles it.
        _ => PaymentClientProblem.Unavailable
    };

    private static (string? Code, string? Detail) ReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return (
                root.TryGetProperty("code", out var code) ? code.GetString() : null,
                root.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, payload.Length > 200 ? payload[..200] : payload);
        }
    }
}
