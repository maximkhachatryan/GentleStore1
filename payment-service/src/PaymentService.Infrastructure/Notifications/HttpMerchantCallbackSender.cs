using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PaymentService.Application.Notifications;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.Notifications;

/// <summary>
/// Posts one outbox message to the caller's callback URL.
/// </summary>
public sealed class HttpMerchantCallbackSender : IMerchantCallbackSender
{
    public const string HttpClientName = "merchant-callbacks";

    private readonly IHttpClientFactory _clients;
    private readonly PaymentServiceOptions _options;

    public HttpMerchantCallbackSender(IHttpClientFactory clients, IOptions<PaymentServiceOptions> options)
    {
        _clients = clients;
        _options = options.Value;
    }

    public async Task<CallbackDeliveryResult> SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var client = _clients.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, message.CallbackUrl)
        {
            Content = new StringContent(message.Payload, Encoding.UTF8, "application/json")
        };

        // Enough in the headers to route and deduplicate without parsing the body — the receiver
        // may want to reject a redelivery before it does any work.
        request.Headers.TryAddWithoutValidation("X-Payment-Event", message.EventType);
        request.Headers.TryAddWithoutValidation("X-Payment-Event-Id", message.Id.ToString());
        request.Headers.TryAddWithoutValidation("X-Payment-Id", message.PaymentId.ToString());
        request.Headers.TryAddWithoutValidation("X-Payment-Merchant-Id", message.MerchantId.ToString());
        request.Headers.TryAddWithoutValidation("X-Payment-Attempt", (message.Attempts + 1).ToString());

        if (Sign(message.Payload) is { } signature)
            request.Headers.TryAddWithoutValidation(_options.Callbacks.SignatureHeader, signature);

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return new CallbackDeliveryResult(true, (int)response.StatusCode);

            // A 4xx means the receiver understood and refused, so repeating the identical body will
            // not help. 408 and 429 are the exceptions — both are explicitly "try again".
            var retryable =
                response.StatusCode >= HttpStatusCode.InternalServerError ||
                response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

            return new CallbackDeliveryResult(
                false, (int)response.StatusCode, $"Callback answered {(int)response.StatusCode}.", retryable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new CallbackDeliveryResult(false, null, "Callback timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new CallbackDeliveryResult(false, null, ex.Message);
        }
    }

    private string? Sign(string payload)
    {
        var secret = _options.Callbacks.SigningSecret;
        if (string.IsNullOrWhiteSpace(secret)) return null;

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
