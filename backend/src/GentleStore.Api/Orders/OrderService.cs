using GentleStore.Api.Common;
using GentleStore.Api.Contracts;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Orders;

public enum PlaceOrderProblem
{
    None,
    EmptyCart,
    TooManyItems,
    InvalidQuantity,
    InvalidFulfilment,
    AddressRequired,
    ContactRequired,
    PhoneMissingCountryCode,
    PhoneInvalid,
    UnknownProduct,
    ProductUnavailable,
    VariantRequired,
    UnknownVariant,
    VariantUnavailable,
    CustomerBlocked
}

public sealed record PlaceOrderResult(PlaceOrderProblem Problem, Order? Order);

public interface IOrderService
{
    Task<PlaceOrderResult> PlaceAsync(Store store, PlaceOrderRequest req, CancellationToken ct = default);

    /// <summary>Orders this browser is allowed to see. Scope depends on how it was identified.</summary>
    Task<List<Order>> HistoryAsync(Store store, CustomerSession session, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private const int MaxLines = 50;
    private const int MaxQuantity = 999;

    private readonly AppDbContext _db;
    private readonly IStorefrontGate _gate;

    public OrderService(AppDbContext db, IStorefrontGate gate)
    {
        _db = db;
        _gate = gate;
    }

    public async Task<PlaceOrderResult> PlaceAsync(Store store, PlaceOrderRequest req, CancellationToken ct = default)
    {
        var items = Consolidate(req.Items);
        if (items.Count == 0) return Fail(PlaceOrderProblem.EmptyCart);
        if (items.Count > MaxLines) return Fail(PlaceOrderProblem.TooManyItems);
        if (items.Any(i => i.Quantity is < 1 or > MaxQuantity)) return Fail(PlaceOrderProblem.InvalidQuantity);

        if (!Enum.TryParse<FulfilmentMethod>(req.Fulfilment, ignoreCase: true, out var fulfilment))
            return Fail(PlaceOrderProblem.InvalidFulfilment);

        var address = Clean(req.DeliveryAddress);
        if (fulfilment == FulfilmentMethod.Delivery && address is null)
            return Fail(PlaceOrderProblem.AddressRequired);

        // Catalogue validation runs first because it is read-only, while resolving identity can
        // register a customer and bind a cookie. A cart the store cannot fulfil must not leave a
        // stranger in the customer list behind it.
        var (lines, lineProblem) = await BuildLinesAsync(store, items, ct);
        if (lineProblem != PlaceOrderProblem.None) return Fail(lineProblem);

        var (identity, identityProblem) = await ResolveIdentityAsync(store, req, ct);
        if (identityProblem != PlaceOrderProblem.None) return Fail(identityProblem);

        var now = DateTime.UtcNow;
        // A single unpriced line makes the whole total unknown — the store has to quote it before
        // the customer can agree to anything.
        var priced = lines!.All(l => l.UnitPrice is not null);

        // The order number is allocated and the order inserted in one transaction, so a number is
        // never burnt by a failed insert and two checkouts can never share one.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            StoreId = store.Id,
            OrderNumber = await NextOrderNumberAsync(store, ct),
            CustomerId = identity!.CustomerId,
            CustomerSessionId = identity.SessionId,
            IdentityTier = identity.Tier,
            Status = priced ? OrderStatus.New : OrderStatus.AwaitingQuote,
            ContactName = identity.ContactName,
            ContactPhone = identity.ContactPhone,
            ContactPhoneNormalized = identity.ContactPhoneNormalized,
            Fulfilment = fulfilment,
            DeliveryAddress = fulfilment == FulfilmentMethod.Delivery ? address : null,
            CustomerNote = Clean(req.Note),
            Currency = store.Currency,
            Total = priced ? lines.Sum(l => l.UnitPrice!.Value * l.Quantity) : null,
            PlacedAt = now,
            UpdatedAt = now,
            Lines = lines
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PlaceOrderResult(PlaceOrderProblem.None, order);
    }

    public Task<List<Order>> HistoryAsync(Store store, CustomerSession session, CancellationToken ct = default)
    {
        var query = _db.Orders.Where(o => o.StoreId == store.Id);

        // A binding created by a store-issued invite is vouched for, so it may see everything that
        // customer ever ordered. A self-declared binding only proves "this browser placed these
        // orders" — anything else would let someone who guesses a phone number read a stranger's
        // history at checkout.
        query = session.CustomerInviteId is not null
            ? query.Where(o => o.CustomerId == session.CustomerId)
            : query.Where(o => o.CustomerSessionId == session.Id);

        return query
            .Include(o => o.Lines)
            .OrderByDescending(o => o.PlacedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    private sealed record OrderIdentity(
        Guid CustomerId, Guid? SessionId, CustomerIdentityTier Tier,
        string ContactName, string ContactPhone, string ContactPhoneNormalized);

    private async Task<(OrderIdentity? Identity, PlaceOrderProblem Problem)> ResolveIdentityAsync(
        Store store, PlaceOrderRequest req, CancellationToken ct)
    {
        var session = await _gate.PeekSessionAsync(store, ct);

        if (session?.Customer is not null)
        {
            var customer = session.Customer;
            if (customer.IsBlocked) return (null, PlaceOrderProblem.CustomerBlocked);

            // The store may have registered a phone number and no name; in that case the customer
            // still has to tell us who to ask for.
            var name = Clean(customer.FullName) ?? Clean(req.ContactName);
            if (name is null) return (null, PlaceOrderProblem.ContactRequired);

            var tier = session.CustomerInviteId is not null
                ? CustomerIdentityTier.Invited
                : await _db.Orders.AnyAsync(o => o.CustomerSessionId == session.Id, ct)
                    ? CustomerIdentityTier.Returning
                    : CustomerIdentityTier.Guest;

            return (new OrderIdentity(
                customer.Id, session.Id, tier, name, customer.Phone, customer.PhoneNormalized),
                PlaceOrderProblem.None);
        }

        // Guest checkout: the only thing we know is what was just typed into the form.
        var guestName = Clean(req.ContactName);
        var guestPhone = Clean(req.ContactPhone);
        if (guestName is null || guestPhone is null) return (null, PlaceOrderProblem.ContactRequired);

        if (!PhoneNumbers.TryNormalize(guestPhone, out var normalized, out var phoneProblem))
            return (null, phoneProblem == PhoneNumbers.Problem.MissingCountryCode
                ? PlaceOrderProblem.PhoneMissingCountryCode
                : PlaceOrderProblem.PhoneInvalid);

        var registration = await _gate.RegisterGuestAsync(store, guestPhone, normalized, guestName, ct);
        if (registration.Outcome == GuestOutcome.CustomerBlocked || registration.Session is null)
            return (null, PlaceOrderProblem.CustomerBlocked);

        var guestCustomer = registration.Customer!;
        var guestTier = await _db.Orders.AnyAsync(o => o.CustomerSessionId == registration.Session.Id, ct)
            ? CustomerIdentityTier.Returning
            : CustomerIdentityTier.Guest;

        return (new OrderIdentity(
            guestCustomer.Id, registration.Session.Id, guestTier,
            guestName, guestPhone, normalized),
            PlaceOrderProblem.None);
    }

    private async Task<(List<OrderLine>? Lines, PlaceOrderProblem Problem)> BuildLinesAsync(
        Store store, List<PlaceOrderItemRequest> items, CancellationToken ct)
    {
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => p.StoreId == store.Id && productIds.Contains(p.Id))
            .Include(p => p.Variants).ThenInclude(v => v.Attributes)
            .ToDictionaryAsync(p => p.Id, ct);

        var lines = new List<OrderLine>();
        var order = 0;

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                return (null, PlaceOrderProblem.UnknownProduct);
            if (!product.IsAvailable)
                return (null, PlaceOrderProblem.ProductUnavailable);

            decimal? unitPrice;
            string? variantLabel = null;
            Guid? variantId = null;

            if (product.Variants.Count > 0)
            {
                // With variants defined, the bare product is not a sellable thing — a size has to
                // be chosen before there is a price to charge.
                if (item.VariantId is null) return (null, PlaceOrderProblem.VariantRequired);

                var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId);
                if (variant is null) return (null, PlaceOrderProblem.UnknownVariant);
                if (!variant.IsAvailable) return (null, PlaceOrderProblem.VariantUnavailable);

                variantId = variant.Id;
                unitPrice = variant.Price;
                variantLabel = string.Join(", ", variant.Attributes
                    .OrderBy(a => a.Name)
                    .Select(a => $"{a.Name}: {a.Value}"));
                if (variantLabel.Length == 0) variantLabel = null;
            }
            else
            {
                if (item.VariantId is not null) return (null, PlaceOrderProblem.UnknownVariant);
                unitPrice = product.Price;
            }

            lines.Add(new OrderLine
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductVariantId = variantId,
                ProductName = product.Name,
                VariantLabel = variantLabel,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                DisplayOrder = order++
            });
        }

        return (lines, PlaceOrderProblem.None);
    }

    /// <summary>
    /// Reserves the next number for this store. Must be called inside a transaction: the UPDATE
    /// takes a row lock on the store that is held until commit, so a concurrent checkout blocks
    /// rather than reading the same counter value. The read that follows sees this transaction's
    /// own uncommitted increment.
    /// </summary>
    private async Task<string> NextOrderNumberAsync(Store store, CancellationToken ct)
    {
        await _db.Stores
            .Where(s => s.Id == store.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.OrderSequence, s => s.OrderSequence + 1), ct);

        var sequence = await _db.Stores
            .AsNoTracking()
            .Where(s => s.Id == store.Id)
            .Select(s => s.OrderSequence)
            .FirstAsync(ct);

        return $"{NumberPrefix(store.Slug)}-{sequence:0000}";
    }

    /// <summary>Initials of the slug, so "bean-scene" orders read as "BS-0001".</summary>
    private static string NumberPrefix(string slug)
    {
        var initials = new string(slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word[0])
            .Where(char.IsAsciiLetterOrDigit)
            .Take(3)
            .ToArray())
            .ToUpperInvariant();

        if (initials.Length >= 2) return initials;

        var fallback = new string(slug.Where(char.IsAsciiLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
        return fallback.Length > 0 ? fallback : "OR";
    }

    /// <summary>Folds repeated product/variant pairs into one line so the order reads cleanly.</summary>
    private static List<PlaceOrderItemRequest> Consolidate(List<PlaceOrderItemRequest>? items) =>
        (items ?? [])
        .GroupBy(i => (i.ProductId, i.VariantId))
        .Select(g => new PlaceOrderItemRequest(g.Key.ProductId, g.Key.VariantId, g.Sum(i => i.Quantity)))
        .ToList();

    private static PlaceOrderResult Fail(PlaceOrderProblem problem) => new(problem, null);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
