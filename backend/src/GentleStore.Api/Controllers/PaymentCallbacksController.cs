using System.Text.Json;
using GentleStore.Api.Orders;
using GentleStore.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GentleStore.Api.Controllers;

/// <summary>
/// Where PaymentService reports that an order has been paid.
/// <para>
/// Anonymous, because the caller is a service rather than a person — and therefore signed. The body
/// is an assertion that money moved, so whatever can post here can release orders; the HMAC is what
/// makes that safe independently of how the network happens to be arranged today.
/// </para>
/// <para>
/// Answers 200 for anything it understands, including callbacks about orders it does not have.
/// PaymentService retries until it gets a 2xx, and there is nothing to retry when the answer will
/// not change.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/payments/callback")]
public class PaymentCallbacksController : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOrderPaymentService _payments;
    private readonly IPaymentCallbackVerifier _verifier;
    private readonly ILogger<PaymentCallbacksController> _log;

    public PaymentCallbacksController(
        IOrderPaymentService payments,
        IPaymentCallbackVerifier verifier,
        ILogger<PaymentCallbacksController> log)
    {
        _payments = payments;
        _verifier = verifier;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        var ct = HttpContext.RequestAborted;

        // Read as raw text: the signature covers the exact bytes that were sent, so binding to a
        // model and re-serialising would break verification.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        if (body.Length > MaxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { code = "payload_too_large" });

        if (!_verifier.Verify(body, Request.Headers[PaymentCallbackVerifier.HeaderName]))
        {
            _log.LogWarning("Rejected a payment callback with a bad or missing signature.");
            return Unauthorized(new { code = "invalid_signature" });
        }

        PaymentCallbackPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PaymentCallbackPayload>(body, Json);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Could not read a payment callback body.");
            return BadRequest(new { code = "unparseable" });
        }

        if (payload is null) return BadRequest(new { code = "unparseable" });

        var matched = await _payments.ApplyCallbackAsync(payload, ct);

        if (!matched)
            _log.LogInformation(
                "Payment callback {Event} for payment {Payment} matched no order here.",
                payload.EventType, payload.PaymentId);

        return Ok(new { status = matched ? "applied" : "ignored" });
    }
}
