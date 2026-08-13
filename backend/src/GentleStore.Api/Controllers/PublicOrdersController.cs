using GentleStore.Api.Common;
using GentleStore.Api.Contracts;
using GentleStore.Api.Orders;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GentleStore.Api.Controllers;

[Route("api/public/stores/{slug}/orders")]
public class PublicOrdersController : PublicStoreControllerBase
{
    private readonly IOrderService _orders;

    public PublicOrdersController(IStorefrontGate gate, IOrderService orders) : base(gate) => _orders = orders;

    /// <summary>
    /// Places an order. On an invite-only storefront the gate has already established who this is;
    /// on a public one the customer introduces themselves here and the checkout doubles as a
    /// registration.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PlaceOrder)]
    public async Task<IActionResult> Place(string slug, PlaceOrderRequest req)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var result = await _orders.PlaceAsync(store!, req, HttpContext.RequestAborted);
        if (result.Problem != PlaceOrderProblem.None)
            return Problem(result.Problem);

        var order = result.Order!;
        return Created($"/api/public/stores/{slug}/orders/{order.Id}", ToDto(order));
    }

    /// <summary>Orders this browser may see — see <see cref="IOrderService.HistoryAsync"/> for the scope rule.</summary>
    [HttpGet]
    public async Task<IActionResult> List(string slug)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var session = await Gate.PeekSessionAsync(store!, HttpContext.RequestAborted);
        if (session is null) return Ok(Array.Empty<PublicOrderDto>());

        var orders = await _orders.HistoryAsync(store!, session, HttpContext.RequestAborted);
        return Ok(orders.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(string slug, Guid id)
    {
        var (store, _, error) = await OpenStorefrontAsync(slug);
        if (error is not null) return error;

        var session = await Gate.PeekSessionAsync(store!, HttpContext.RequestAborted);
        if (session is null) return NotFound();

        // Read through the same scoped set as the list, so a guessed id cannot widen access.
        var orders = await _orders.HistoryAsync(store!, session, HttpContext.RequestAborted);
        var order = orders.FirstOrDefault(o => o.Id == id);
        return order is null ? NotFound() : Ok(ToDto(order));
    }

    private static PublicOrderDto ToDto(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.Status.ToString(),
        order.Currency,
        order.Total,
        order.Fulfilment.ToString(),
        order.DeliveryAddress,
        order.CustomerNote,
        order.ContactName,
        PhoneNumbers.Mask(order.ContactPhoneNormalized),
        AwaitingQuote: order.Status == OrderStatus.AwaitingQuote,
        order.PlacedAt,
        order.Lines
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new PublicOrderLineDto(
                l.ProductId, l.ProductName, l.VariantLabel, l.Quantity, l.UnitPrice, l.LineTotal))
            .ToList());

    /// <summary>Stable codes the storefront maps to its own copy, rather than English prose.</summary>
    private IActionResult Problem(PlaceOrderProblem problem)
    {
        var code = problem switch
        {
            PlaceOrderProblem.EmptyCart => "cart_empty",
            PlaceOrderProblem.TooManyItems => "cart_too_large",
            PlaceOrderProblem.InvalidQuantity => "invalid_quantity",
            PlaceOrderProblem.InvalidFulfilment => "invalid_fulfilment",
            PlaceOrderProblem.AddressRequired => "address_required",
            PlaceOrderProblem.ContactRequired => "contact_required",
            PlaceOrderProblem.PhoneMissingCountryCode => "phone_missing_country_code",
            PlaceOrderProblem.PhoneInvalid => "phone_invalid",
            PlaceOrderProblem.UnknownProduct => "product_unknown",
            PlaceOrderProblem.ProductUnavailable => "product_unavailable",
            PlaceOrderProblem.VariantRequired => "variant_required",
            PlaceOrderProblem.UnknownVariant => "variant_unknown",
            PlaceOrderProblem.VariantUnavailable => "variant_unavailable",
            _ => "customer_blocked"
        };

        return problem == PlaceOrderProblem.CustomerBlocked
            ? StatusCode(StatusCodes.Status403Forbidden, new { code })
            // 409 for the stale-cart cases: the catalogue moved under the customer's feet, and the
            // storefront should refresh rather than treat it as a bad request.
            : problem is PlaceOrderProblem.ProductUnavailable or PlaceOrderProblem.VariantUnavailable
                ? Conflict(new { code })
                : BadRequest(new { code });
    }
}
