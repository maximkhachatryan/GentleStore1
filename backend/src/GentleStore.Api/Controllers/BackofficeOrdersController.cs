using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

[Route("api/backoffice/orders")]
public class BackofficeOrdersController : BackofficeControllerBase
{
    /// <summary>
    /// Which status a store may move an order to next. Keeps the list honest: an order cannot go
    /// back to New, and a completed or cancelled one is finished.
    /// </summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.New] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.AwaitingQuote] = [OrderStatus.Cancelled],
        [OrderStatus.Quoted] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Ready, OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = []
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public BackofficeOrdersController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> List(string? search, string? status)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var query = _db.Orders.Where(o => o.StoreId == storeId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(new { error = "Unknown order status." });
            query = query.Where(o => o.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderNumber, $"%{term}%")
                || EF.Functions.ILike(o.ContactName, $"%{term}%")
                || (digits.Length > 0 && EF.Functions.ILike(o.ContactPhoneNormalized, $"%{digits}%")));
        }

        var orders = await query
            .OrderByDescending(o => o.PlacedAt)
            .Take(200)
            .Select(o => new OrderListItemDto(
                o.Id, o.OrderNumber, o.Status.ToString(), o.IdentityTier.ToString(),
                o.CustomerId, o.ContactName, o.ContactPhone,
                o.Currency, o.Total, o.Lines.Count, o.Fulfilment.ToString(),
                o.Customer!.Orders.Count, o.PlacedAt))
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var order = await LoadAsync(id, storeId);
        return order is null ? NotFound() : Ok(await ToDetailAsync(order));
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        if (!Enum.TryParse<OrderStatus>(req.Status, ignoreCase: true, out var target))
            return BadRequest(new { error = "Unknown order status.", code = "status_unknown" });

        var order = await LoadAsync(id, storeId);
        if (order is null) return NotFound();

        if (!Transitions[order.Status].Contains(target))
            return Conflict(new
            {
                error = $"An order that is {order.Status} cannot become {target}.",
                code = "status_transition_invalid"
            });

        order.Status = target;
        if (req.StoreNote is not null)
            order.StoreNote = string.IsNullOrWhiteSpace(req.StoreNote) ? null : req.StoreNote.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(await ToDetailAsync(order));
    }

    /// <summary>Prices the "price on request" lines so the customer has a total to agree to.</summary>
    [HttpPut("{id:guid}/quote")]
    public async Task<IActionResult> Quote(Guid id, QuoteOrderRequest req)
    {
        if (_current.StoreId is not Guid storeId) return Forbidden();

        var order = await LoadAsync(id, storeId);
        if (order is null) return NotFound();
        if (order.Status is not (OrderStatus.AwaitingQuote or OrderStatus.Quoted))
            return Conflict(new { error = "Only an order awaiting a quote can be priced.", code = "not_quotable" });

        foreach (var update in req.Lines ?? [])
        {
            if (update.UnitPrice < 0)
                return BadRequest(new { error = "Prices cannot be negative.", code = "price_invalid" });

            var line = order.Lines.FirstOrDefault(l => l.Id == update.LineId);
            if (line is null) return BadRequest(new { error = "Unknown order line.", code = "line_unknown" });
            line.UnitPrice = update.UnitPrice;
        }

        if (order.Lines.Any(l => l.UnitPrice is null))
            return BadRequest(new { error = "Every line needs a price.", code = "quote_incomplete" });

        order.Total = order.Lines.Sum(l => l.UnitPrice!.Value * l.Quantity);
        order.Status = OrderStatus.Quoted;
        if (req.StoreNote is not null)
            order.StoreNote = string.IsNullOrWhiteSpace(req.StoreNote) ? null : req.StoreNote.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(await ToDetailAsync(order));
    }

    private Task<Order?> LoadAsync(Guid id, Guid storeId) =>
        _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id && o.StoreId == storeId);

    private async Task<OrderDetailDto> ToDetailAsync(Order order)
    {
        var customerOrderCount = await _db.Orders.CountAsync(o => o.CustomerId == order.CustomerId);

        return new OrderDetailDto(
            order.Id, order.OrderNumber, order.Status.ToString(), order.IdentityTier.ToString(),
            order.CustomerId, order.ContactName, order.ContactPhone, order.ContactPhoneNormalized,
            (order.Customer?.Origin ?? CustomerOrigin.SelfRegistered).ToString(), customerOrderCount,
            order.Fulfilment.ToString(), order.DeliveryAddress, order.CustomerNote, order.StoreNote,
            order.Currency, order.Total, order.PlacedAt, order.UpdatedAt,
            order.Lines
                .OrderBy(l => l.DisplayOrder)
                .Select(l => new OrderLineDto(
                    l.Id, l.ProductId, l.ProductVariantId, l.ProductName, l.VariantLabel,
                    l.Quantity, l.UnitPrice, l.LineTotal))
                .ToList());
    }
}
