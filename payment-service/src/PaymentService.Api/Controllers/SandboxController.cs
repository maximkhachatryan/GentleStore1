using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Contracts;
using PaymentService.Application.Payments;
using PaymentService.Infrastructure.Gateways.Sandbox;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Api.Controllers;

/// <summary>
/// Stands in for a hosted checkout page while there is no real gateway.
/// <para>
/// Only reachable when <c>Gateways:sandbox:Enabled</c> is set; otherwise every action here answers
/// 404. Delete this along with <see cref="SandboxPaymentGateway"/> once a real provider is wired
/// up — nothing else depends on it.
/// </para>
/// </summary>
[ApiController]
[Route("api/sandbox")]
public class SandboxController : ControllerBase
{
    private readonly SandboxPaymentGateway? _sandbox;
    private readonly PaymentsDbContext _db;
    private readonly IPaymentOrchestrator _payments;

    public SandboxController(PaymentsDbContext db, IPaymentOrchestrator payments, SandboxPaymentGateway? sandbox = null)
    {
        _db = db;
        _payments = payments;
        _sandbox = sandbox;
    }

    /// <summary>The fake checkout page a customer would be redirected to.</summary>
    [HttpGet("checkout/{gatewayPaymentId}")]
    public IActionResult Checkout(string gatewayPaymentId)
    {
        if (_sandbox is null) return NotFound();

        var html = $"""
            <!doctype html>
            <meta charset="utf-8">
            <title>Sandbox checkout</title>
            <body style="font-family:system-ui;max-width:32rem;margin:4rem auto">
              <h1>Sandbox checkout</h1>
              <p>Payment <code>{gatewayPaymentId}</code>. This is a test double, not a payment page.</p>
              <form method="post" action="/api/sandbox/checkout/{gatewayPaymentId}/confirm?success=true">
                <button type="submit">Pay</button>
              </form>
              <form method="post" action="/api/sandbox/checkout/{gatewayPaymentId}/confirm?success=false">
                <button type="submit">Fail</button>
              </form>
            </body>
            """;

        return Content(html, "text/html");
    }

    /// <summary>
    /// Completes the fake payment and pulls the result through the normal sync path, so the
    /// payment, its attempts and its callback all behave exactly as they would for a real gateway.
    /// </summary>
    [HttpPost("checkout/{gatewayPaymentId}/confirm")]
    public async Task<IActionResult> Confirm(string gatewayPaymentId, [FromQuery] bool success = true)
    {
        if (_sandbox is null) return NotFound();
        if (!_sandbox.Confirm(gatewayPaymentId, success)) return NotFound(new { code = "unknown_sandbox_payment" });

        var payment = await _db.Payments.FirstOrDefaultAsync(
            p => p.GatewayKey == "sandbox" && p.GatewayPaymentId == gatewayPaymentId, HttpContext.RequestAborted);

        if (payment is null) return NotFound(new { code = "payment_not_found" });

        var result = await _payments.SyncAsync(payment.MerchantId, payment.Id, HttpContext.RequestAborted);

        var returnUrl = _sandbox.ReturnUrlOf(gatewayPaymentId);
        if (returnUrl is not null) return Redirect(returnUrl);

        return result.IsSuccess ? Ok(result.Value!.ToDto()) : Ok(payment.ToDto());
    }
}
