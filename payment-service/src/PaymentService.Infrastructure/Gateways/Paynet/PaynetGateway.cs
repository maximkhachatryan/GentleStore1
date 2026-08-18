using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.Gateways.Paynet;

/// <summary>
/// Paynet (paynet.am) — a single Armenian aggregator in front of ARCA cards, Idram and Telcell.
/// <para>
/// One instance per processor slug, registered under <c>paynet-arca</c>, <c>paynet-idram</c> and
/// so on. Stateless and thread-safe: the merchant's API key arrives on
/// <see cref="GatewayContext"/> for every call.
/// </para>
/// <para>
/// Paynet has no authorization/capture split and no void, so this gateway declares neither. The
/// orchestrator checks those capabilities before it calls, which is how a merchant gets a clear
/// "this gateway cannot do that" instead of a Paynet 404.
/// </para>
/// </summary>
public sealed class PaynetGateway : IPaymentGateway
{
    public const string HttpClientName = "paynet";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Languages = ["en", "hy", "ru"];

    private readonly string _processor;
    private readonly IHttpClientFactory _clients;
    private readonly PaymentServiceOptions _service;
    private readonly ILogger<PaynetGateway> _log;

    public PaynetGateway(
        string processor,
        IHttpClientFactory clients,
        IOptions<PaymentServiceOptions> service,
        ILogger<PaynetGateway> log)
    {
        _processor = processor.Trim().ToLowerInvariant();
        _clients = clients;
        _service = service.Value;
        _log = log;
    }

    public string Key => PaynetMapping.GatewayKey(_processor);
    public string DisplayName => PaynetMapping.DisplayName(_processor);

    public GatewayCapabilities Capabilities =>
        GatewayCapabilities.Refunds |
        GatewayCapabilities.PartialRefunds |
        GatewayCapabilities.StatusPolling |
        GatewayCapabilities.Webhooks |
        GatewayCapabilities.HostedRedirect;

    /// <summary>AMD only — Paynet's v1 accepts nothing else.</summary>
    public IReadOnlyCollection<string> SupportedCurrencies => [PaynetAmounts.Currency];

    // ---- Create ----

    public async Task<GatewayResult<GatewayPaymentSnapshot>> CreatePaymentAsync(
        GatewayCreatePaymentRequest request, CancellationToken ct = default)
    {
        if (!PaynetAmounts.TryToDram(request.Amount, out var dram) || dram < 1)
            return GatewayResult<GatewayPaymentSnapshot>.Rejected(new GatewayError(
                GatewayErrorCodes.InvalidRequest,
                $"Paynet takes whole drams of AMD; {request.Amount} cannot be sent."));

        // Paynet requires it, and there is nowhere sensible to send the customer without one.
        if (string.IsNullOrWhiteSpace(request.ReturnUrl))
            return GatewayResult<GatewayPaymentSnapshot>.Rejected(new GatewayError(
                GatewayErrorCodes.InvalidRequest, "Paynet requires a returnUrl on the payment."));

        var credentials = request.Context.Credentials;

        var body = new PaynetCreatePaymentBody
        {
            Amount = dram,
            Currency = PaynetAmounts.Currency,
            // Our reference, not the caller's order number: Paynet requires order_id to be unique
            // per merchant, and a second attempt at the same order would collide on it.
            OrderId = request.Reference,
            Processor = _processor,
            ReturnUrl = request.ReturnUrl,
            CancelUrl = request.CancelUrl,
            CallbackUrl = WebhookUrl(),
            Description = Describe(request),
            CustomerEmail = request.Customer?.Email,
            CustomerName = request.Customer?.Name,
            Domain = credentials.Get("Domain"),
            RedirectMode = credentials.Get("RedirectMode"),
            Language = Language(request.Metadata)
        };

        var call = await CallAsync(request.Context, HttpMethod.Post, "v1/payments", body, request.IdempotencyKey, ct);
        return call.Success ? Snapshot(call.Body!) : call.ToResult<GatewayPaymentSnapshot>();
    }

    // ---- Capture / Cancel: not offered by Paynet ----

    public Task<GatewayResult<GatewayPaymentSnapshot>> CapturePaymentAsync(
        GatewayCaptureRequest request, CancellationToken ct = default) =>
        Task.FromResult(NotSupported("Paynet settles at checkout; there is nothing to capture separately."));

    public Task<GatewayResult<GatewayPaymentSnapshot>> CancelPaymentAsync(
        GatewayCancelRequest request, CancellationToken ct = default) =>
        Task.FromResult(NotSupported(
            "Paynet has no void. An unpaid payment expires on its own; a paid one is undone with a refund."));

    // ---- Fetch ----

    public async Task<GatewayResult<GatewayPaymentSnapshot>> FetchPaymentAsync(
        GatewayFetchRequest request, CancellationToken ct = default)
    {
        var call = await CallAsync(
            request.Context, HttpMethod.Get, $"v1/payments/{Uri.EscapeDataString(request.GatewayPaymentId)}",
            null, null, ct);

        return call.Success ? Snapshot(call.Body!) : call.ToResult<GatewayPaymentSnapshot>();
    }

    // ---- Refund ----

    public async Task<GatewayResult<GatewayRefundSnapshot>> RefundAsync(
        GatewayRefundRequest request, CancellationToken ct = default)
    {
        if (!PaynetAmounts.TryToDram(request.Amount, out var dram) || dram < 1)
            return GatewayResult<GatewayRefundSnapshot>.Rejected(new GatewayError(
                GatewayErrorCodes.InvalidRequest,
                $"Paynet refunds whole drams of AMD; {request.Amount} cannot be sent."));

        var body = new PaynetRefundBody { Amount = dram, Reason = Truncate(request.Reason, 500) };

        var call = await CallAsync(
            request.Context, HttpMethod.Post,
            $"v1/payments/{Uri.EscapeDataString(request.GatewayPaymentId)}/refund",
            body, request.IdempotencyKey, ct);

        if (!call.Success) return call.ToResult<GatewayRefundSnapshot>();

        var refund = Deserialize<PaynetRefund>(call.Body!);
        if (refund?.Uuid is null)
            return GatewayResult<GatewayRefundSnapshot>.Permanent(
                new GatewayError(GatewayErrorCodes.Unknown, "Paynet returned a refund without a uuid."), call.Body);

        return GatewayResult<GatewayRefundSnapshot>.Success(PaynetMapping.ToSnapshot(refund), call.Body);
    }

    // ---- Internals ----

    private GatewayResult<GatewayPaymentSnapshot> Snapshot(string body)
    {
        var payment = Deserialize<PaynetPayment>(body);
        if (payment?.Uuid is null)
            return GatewayResult<GatewayPaymentSnapshot>.Permanent(
                new GatewayError(GatewayErrorCodes.Unknown, "Paynet returned a payment without a uuid."), body);

        var snapshot = PaynetMapping.ToSnapshot(payment, _processor, out var recognised);

        if (!recognised)
            _log.LogWarning(
                "Paynet returned status '{Status}' for payment {Uuid}, which this integration does not know. " +
                "Treated as in-flight.", payment.Status, payment.Uuid);

        return GatewayResult<GatewayPaymentSnapshot>.Success(snapshot, body);
    }

    /// <summary>
    /// One HTTP call, with every failure turned into a result rather than an exception. Which
    /// outcome a failure maps to decides whether the orchestrator marks the payment failed or
    /// leaves it in flight, so the classification matters more than the message.
    /// </summary>
    private async Task<PaynetCall> CallAsync(
        GatewayContext context, HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken ct)
    {
        var apiKey = context.Credentials.Get("ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            return PaynetCall.Fail(AttemptOutcome.Rejected, new GatewayError(
                GatewayErrorCodes.CredentialsRejected, "No Paynet ApiKey is configured for this merchant account."));

        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Paynet-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Paynet honours Idempotency-Key for 24 hours and replays the original response, which is
        // what makes a timeout on this call safe to retry rather than a possible double charge.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

        try
        {
            var client = _clients.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            return response.IsSuccessStatusCode
                ? PaynetCall.Ok(payload)
                : Classify(response.StatusCode, payload);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Safe to call transient rather than unknown only because every write we make carries
            // an idempotency key: retrying replays Paynet's original answer instead of charging
            // again. Without that key this would have to be AttemptOutcome.Unknown.
            return PaynetCall.Fail(AttemptOutcome.TransientFailure, new GatewayError(
                GatewayErrorCodes.Timeout, "Paynet did not answer in time.", Retryable: true));
        }
        catch (HttpRequestException ex)
        {
            return PaynetCall.Fail(AttemptOutcome.TransientFailure, new GatewayError(
                GatewayErrorCodes.Unavailable, ex.Message, Retryable: true));
        }
    }

    private PaynetCall Classify(HttpStatusCode status, string payload)
    {
        var error = Deserialize<PaynetError>(payload);
        var message = error?.Describe() ?? $"Paynet answered {(int)status}.";
        var raw = error?.Error;

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PaynetCall.Fail(
                AttemptOutcome.Rejected,
                new GatewayError(GatewayErrorCodes.CredentialsRejected, message, raw), payload),

            HttpStatusCode.NotFound => PaynetCall.Fail(
                AttemptOutcome.Rejected,
                new GatewayError(GatewayErrorCodes.InvalidRequest, "Paynet has no such payment.", raw), payload),

            // Paynet's own "this idempotency key is still being processed". Explicitly retryable —
            // it is telling us to come back, not that anything is wrong.
            HttpStatusCode.Conflict => PaynetCall.Fail(
                AttemptOutcome.TransientFailure,
                new GatewayError(GatewayErrorCodes.Unavailable, message, raw, Retryable: true), payload),

            HttpStatusCode.UnprocessableContent => PaynetCall.Fail(
                AttemptOutcome.Rejected,
                new GatewayError(GatewayErrorCodes.InvalidRequest, message, raw), payload),

            HttpStatusCode.TooManyRequests => PaynetCall.Fail(
                AttemptOutcome.TransientFailure,
                new GatewayError(GatewayErrorCodes.RateLimited, message, raw, Retryable: true), payload),

            >= HttpStatusCode.InternalServerError => PaynetCall.Fail(
                AttemptOutcome.TransientFailure,
                new GatewayError(GatewayErrorCodes.Unavailable, message, raw, Retryable: true), payload),

            _ => PaynetCall.Fail(
                AttemptOutcome.PermanentFailure,
                new GatewayError(GatewayErrorCodes.Unknown, message, raw), payload)
        };
    }

    /// <summary>
    /// Where Paynet posts its notifications. Null when this service has no public origin
    /// configured, in which case the field is omitted and payments are resolved by polling.
    /// </summary>
    private string? WebhookUrl()
    {
        var origin = _service.PublicBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(origin) ? null : $"{origin}/api/webhooks/{Key}";
    }

    /// <summary>What the merchant sees against the payment in Paynet's dashboard.</summary>
    private static string? Describe(GatewayCreatePaymentRequest request)
    {
        var text = request.Description
                   ?? (string.IsNullOrWhiteSpace(request.OrderNumber) ? null : $"Order {request.OrderNumber}");

        return Truncate(text, 500);
    }

    /// <summary>
    /// Localizes Paynet's checkout page. Read from metadata because the port has no language of
    /// its own and this is the caller's preference, not the payment's.
    /// </summary>
    private static string? Language(IReadOnlyDictionary<string, string> metadata) =>
        metadata.TryGetValue("language", out var value) &&
        Languages.Contains(value.Trim().ToLowerInvariant())
            ? value.Trim().ToLowerInvariant()
            : null;

    private static GatewayResult<GatewayPaymentSnapshot> NotSupported(string message) =>
        GatewayResult<GatewayPaymentSnapshot>.Rejected(new GatewayError(GatewayErrorCodes.NotSupported, message));

    private static T? Deserialize<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private sealed record PaynetCall(bool Success, string? Body, AttemptOutcome Outcome, GatewayError? Error)
    {
        public static PaynetCall Ok(string body) => new(true, body, AttemptOutcome.Succeeded, null);

        public static PaynetCall Fail(AttemptOutcome outcome, GatewayError error, string? body = null) =>
            new(false, body, outcome, error);

        public GatewayResult<T> ToResult<T>() where T : class => new(Outcome, null, Error, Body);
    }
}
