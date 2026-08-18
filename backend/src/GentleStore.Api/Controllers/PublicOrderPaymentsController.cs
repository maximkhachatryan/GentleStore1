using GentleStore.Api.Contracts;
using GentleStore.Api.Orders;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GentleStore.Api.Controllers;

/// <summary>
/// Paying for an order, from the storefront.
/// <para>
/// Orders are read through the same scoped set as the rest of the storefront, so a guessed order id
/// cannot be used to start a payment against someone else's order — or to read its state back.
/// </para>
/// </summary>
[Route("api/public/stores/{slug}")]
public class PublicOrderPaymentsController : PublicStoreControllerBase
{
    private readonly IOrderService _orders;
    private readonly IOrderPaymentService _payments;

    public PublicOrderPaymentsController(IStorefrontGate gate, IOrderService orders, IOrderPaymentService payments)
        : base(gate)
    {
        _orders = orders;
        _payments = payments;
    }

    /// <summary>What this store accepts. Empty when the store does not take online payments.</summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> Methods(string slug)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var methods = await _payments.MethodsAsync(store!, HttpContext.RequestAborted);
        return Ok(methods.Select(m => new PublicPaymentMethodDto(m.Key, m.DisplayName)).ToList());
    }

    /// <summary>
    /// Starts or resumes payment. Rate limited on the same policy as checkout — it reaches a
    /// payment gateway, and an unbounded endpoint that does is worth abusing.
    /// </summary>
    [HttpPost("orders/{id:guid}/payment")]
    [EnableRateLimiting(RateLimitPolicies.PlaceOrder)]
    public async Task<IActionResult> Start(string slug, Guid id, StartPaymentRequest? req)
    {
        var (store, order, error) = await OpenOrderAsync(slug, id);
        if (error is not null) return error;

        var origin = $"{Request.Scheme}://{Request.Host}";
        var result = await _payments.StartAsync(store!, order!, req?.Method, origin, HttpContext.RequestAborted);

        return result.IsSuccess ? Ok(Describe(result.Value!)) : Problem(result);
    }

    /// <summary>
    /// Re-reads the payment from the gateway. What the order page calls when a customer returns
    /// from a hosted checkout, because the query string they arrive with proves nothing.
    /// </summary>
    [HttpPost("orders/{id:guid}/payment/refresh")]
    public async Task<IActionResult> Refresh(string slug, Guid id)
    {
        var (store, order, error) = await OpenOrderAsync(slug, id);
        if (error is not null) return error;

        var result = await _payments.RefreshAsync(store!, order!, HttpContext.RequestAborted);

        // A gateway we cannot reach is not a reason to hide the order; the customer still gets
        // whatever we last knew, and the page can offer to try again.
        return result.IsSuccess
            ? Ok(Describe(result.Value!))
            : Ok(new OrderPaymentDto(PublicOrdersController.ToDto(order!), null));
    }

    private async Task<(Store? Store, Order? Order, IActionResult? Error)> OpenOrderAsync(string slug, Guid id)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return (null, null, error);

        var session = await Gate.PeekSessionAsync(store!, HttpContext.RequestAborted);
        if (session is null) return (null, null, NotFound());

        var orders = await _orders.HistoryAsync(store!, session, HttpContext.RequestAborted);
        var order = orders.FirstOrDefault(o => o.Id == id);

        return order is null ? (null, null, NotFound()) : (store, order, null);
    }

    private static OrderPaymentDto Describe(PaymentCheckout checkout) => new(
        PublicOrdersController.ToDto(checkout.Order),
        checkout.ActionKind == PaymentActionKind.None
            ? null
            : new PaymentActionDto(checkout.ActionKind.ToString(), checkout.Url, checkout.ExpiresAt));

    /// <summary>Stable codes the storefront maps to its own copy, rather than English prose.</summary>
    private IActionResult Problem(PaymentCheckoutResult result)
    {
        var code = result.Problem switch
        {
            PaymentCheckoutProblem.PaymentsUnavailable => "payments_unavailable",
            PaymentCheckoutProblem.NotPayable => "not_payable",
            PaymentCheckoutProblem.AwaitingQuote => "awaiting_quote",
            PaymentCheckoutProblem.MethodUnavailable => "method_unavailable",
            PaymentCheckoutProblem.Declined => "declined",
            _ => "payment_unavailable"
        };

        return result.Problem switch
        {
            // The customer's card said no. Their problem to solve, and a different one from ours.
            PaymentCheckoutProblem.Declined => StatusCode(StatusCodes.Status402PaymentRequired, new { code }),
            PaymentCheckoutProblem.Unavailable or PaymentCheckoutProblem.PaymentsUnavailable =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, new { code }),
            _ => Conflict(new { code })
        };
    }
}
