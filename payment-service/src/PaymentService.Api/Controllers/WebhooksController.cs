using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Webhooks;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Where gateways deliver notifications.
/// <para>
/// The one endpoint on this service that faces the public internet, and the one the internal-key
/// check is exempt from: a gateway has no key to present. Authenticity comes from the signature on
/// the delivery, verified by the gateway's own integration.
/// </para>
/// <para>
/// The status codes are chosen for how gateways behave, not for tidiness: 200 stops redelivery,
/// 4xx stops it too (the body will never be accepted), and 5xx asks for a retry.
/// </para>
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private const int MaxBodyBytes = 1024 * 1024;

    private readonly IWebhookIngestor _ingestor;

    public WebhooksController(IWebhookIngestor ingestor) => _ingestor = ingestor;

    [HttpPost("{gatewayKey}")]
    public async Task<IActionResult> Receive(string gatewayKey)
    {
        var ct = HttpContext.RequestAborted;

        // Read as raw text and hand the exact bytes on. Signatures are computed over what the
        // gateway sent, so binding to a model and re-serializing would break verification.
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);

        if (body.Length > MaxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { code = "payload_too_large" });

        var delivery = new WebhookDelivery(
            body,
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase));

        var result = await _ingestor.IngestAsync(gatewayKey, delivery, ct);

        return result.Status switch
        {
            // Applied, already seen, or deliberately not interesting — all "stop sending this".
            WebhookIngestStatus.Applied or
            WebhookIngestStatus.Ignored or
            WebhookIngestStatus.Duplicate =>
                Ok(new { status = result.Status.ToString().ToLowerInvariant(), eventId = result.EventId }),

            // Recorded and refused. A retry would carry the identical body and fail identically.
            WebhookIngestStatus.InvalidSignature =>
                BadRequest(new { code = "invalid_signature" }),

            WebhookIngestStatus.Unparseable =>
                BadRequest(new { code = "unparseable", detail = result.Detail }),

            WebhookIngestStatus.UnknownGateway =>
                NotFound(new { code = "gateway_unknown", detail = gatewayKey }),

            // Stored but not applied. 500 asks the gateway to retry; the sweep will also replay it
            // from the stored payload if the gateway gives up first.
            _ => StatusCode(StatusCodes.Status500InternalServerError,
                new { code = "processing_failed", eventId = result.EventId, detail = result.Detail })
        };
    }
}
