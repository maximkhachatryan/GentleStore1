using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Common;
using PaymentService.Api.Contracts;
using PaymentService.Api.Idempotency;
using PaymentService.Application.Payments;
using PaymentService.Domain.Enums;

namespace PaymentService.Api.Controllers;

/// <summary>
/// The API GentleStore calls.
/// <para>
/// <c>merchantId</c> is required on every read and is the tenant boundary: there is no session to
/// derive it from, so it is stated explicitly and every query is filtered by it. If this service
/// is ever exposed beyond an internal network, that parameter is the first thing that has to come
/// from an authenticated principal instead of the caller.
/// </para>
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentOrchestrator _payments;

    public PaymentsController(IPaymentOrchestrator payments) => _payments = payments;

    /// <summary>
    /// Creates a payment and registers it with a gateway.
    /// <para>
    /// Send an <c>Idempotency-Key</c> header. A checkout POST is the request most likely to be
    /// retried, and without a key each retry is another charge.
    /// </para>
    /// <para>
    /// A 201 does not mean paid. Check <c>payment.status</c> and <c>action</c>: anything other than
    /// <c>Captured</c> means the customer still has something to do, or the money is only held.
    /// </para>
    /// </summary>
    [HttpPost]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(InitiatedPaymentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePaymentRequest request)
    {
        if (request.MerchantId == Guid.Empty)
            return BadRequest(new { code = "merchant_required" });

        var captureMode = CaptureMode.Automatic;
        if (!string.IsNullOrWhiteSpace(request.CaptureMode) &&
            !Enum.TryParse<CaptureMode>(request.CaptureMode, ignoreCase: true, out captureMode))
            return BadRequest(new { code = "invalid_capture_mode" });

        var command = new InitiatePaymentCommand(
            request.MerchantId,
            request.Amount,
            request.Currency,
            request.OrderId,
            request.OrderNumber,
            request.Description,
            request.GatewayKey,
            captureMode,
            request.Customer is null
                ? null
                : new PaymentCustomer(request.Customer.Id, request.Customer.Name, request.Customer.Email, request.Customer.Phone),
            request.ReturnUrl,
            request.CancelUrl,
            request.CallbackUrl,
            request.Metadata,
            Request.Headers[IdempotencyFilter.HeaderName].ToString() is { Length: > 0 } key ? key : null);

        var result = await _payments.InitiateAsync(command, HttpContext.RequestAborted);
        if (!result.IsSuccess) return result.ToActionResult(this);

        var initiated = result.Value!;
        var dto = new InitiatedPaymentDto(initiated.Payment.ToDto(), initiated.Action.ToDto());

        return Created($"/api/payments/{initiated.Payment.Id}?merchantId={initiated.Payment.MerchantId}", dto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid merchantId)
    {
        var payment = await _payments.GetAsync(merchantId, id, HttpContext.RequestAborted);
        return payment is null ? NotFound(new { code = "payment_not_found" }) : Ok(payment.ToDto());
    }

    /// <summary>Lookup by the short code that appears on receipts and in support conversations.</summary>
    [HttpGet("by-reference/{reference}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByReference(string reference, [FromQuery] Guid merchantId)
    {
        var payment = await _payments.GetByReferenceAsync(merchantId, reference, HttpContext.RequestAborted);
        return payment is null ? NotFound(new { code = "payment_not_found" }) : Ok(payment.ToDto());
    }

    /// <summary>
    /// Payments for a merchant, newest first. Filter by <c>orderId</c> to answer the question
    /// GentleStore actually asks — "has this order been paid for?".
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedDto<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid merchantId,
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? gatewayKey = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        PaymentStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PaymentStatus>(status, ignoreCase: true, out var value))
                return BadRequest(new { code = "invalid_status" });
            parsed = value;
        }

        var result = await _payments.ListAsync(
            new PaymentQuery(merchantId, orderId, customerId, parsed, gatewayKey, from, to, page, pageSize),
            HttpContext.RequestAborted);

        return Ok(new PagedDto<PaymentDto>(
            result.Items.Select(p => p.ToDto()).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    /// <summary>Takes funds held by an earlier authorization. Manual-capture payments only.</summary>
    [HttpPost("{id:guid}/capture")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Capture(Guid id, [FromQuery] Guid merchantId, CapturePaymentRequest? request)
    {
        var result = await _payments.CaptureAsync(
            merchantId, id, new CapturePaymentCommand(request?.Amount), HttpContext.RequestAborted);

        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.ToActionResult(this);
    }

    /// <summary>Releases an authorization, or abandons a payment the customer never completed.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] Guid merchantId, CancelPaymentRequest? request)
    {
        var result = await _payments.CancelAsync(
            merchantId, id, new CancelPaymentCommand(request?.Reason), HttpContext.RequestAborted);

        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.ToActionResult(this);
    }

    /// <summary>
    /// Re-reads the payment from the gateway.
    /// <para>
    /// Call this when a customer comes back from a hosted checkout: the query string they return
    /// with was written by the browser and can say anything. This asks the gateway.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/sync")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(Guid id, [FromQuery] Guid merchantId)
    {
        var result = await _payments.SyncAsync(merchantId, id, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.ToActionResult(this);
    }

    /// <summary>Returns money. Omit <c>amount</c> to refund everything still refundable.</summary>
    [HttpPost("{id:guid}/refunds")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(RefundDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Refund(Guid id, [FromQuery] Guid merchantId, CreateRefundRequest? request)
    {
        var command = new RefundPaymentCommand(
            request?.Amount,
            request?.Reason,
            request?.Metadata,
            Request.Headers[IdempotencyFilter.HeaderName].ToString() is { Length: > 0 } key ? key : null);

        var result = await _payments.RefundAsync(merchantId, id, command, HttpContext.RequestAborted);
        if (!result.IsSuccess) return result.ToActionResult(this);

        var refund = result.Value!;
        return Created($"/api/payments/{id}/refunds/{refund.Id}?merchantId={merchantId}", refund.ToDto());
    }

    [HttpGet("{id:guid}/refunds")]
    [ProducesResponseType(typeof(IReadOnlyList<RefundDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRefunds(Guid id, [FromQuery] Guid merchantId)
    {
        var payment = await _payments.GetAsync(merchantId, id, HttpContext.RequestAborted);
        if (payment is null) return NotFound(new { code = "payment_not_found" });

        return Ok(payment.Refunds.OrderBy(r => r.CreatedAt).Select(r => r.ToDto()).ToList());
    }
}
