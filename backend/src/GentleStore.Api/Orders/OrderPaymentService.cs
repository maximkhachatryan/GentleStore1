using GentleStore.Api.Notifications;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Payments;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Orders;

public enum PaymentCheckoutProblem
{
    None,

    /// <summary>This deployment has no payment service, or this store does not take payments.</summary>
    PaymentsUnavailable,

    /// <summary>The order is not in a state where money is owed — already paid, cancelled, or never chargeable.</summary>
    NotPayable,

    /// <summary>Still waiting for the store to price a "price on request" line.</summary>
    AwaitingQuote,

    /// <summary>The store does not offer the method that was asked for.</summary>
    MethodUnavailable,

    /// <summary>The issuer refused. A retry means a new payment, not a repeat of this one.</summary>
    Declined,

    /// <summary>The payment service or the gateway is unreachable. Worth trying again.</summary>
    Unavailable
}

public sealed record PaymentCheckout(Order Order, PaymentActionKind ActionKind, string? Url, DateTime? ExpiresAt);

public enum PaymentActionKind { None, Redirect, Display, ClientSdk }

public sealed record PaymentCheckoutResult(
    PaymentCheckoutProblem Problem, PaymentCheckout? Value, string? Detail = null)
{
    public bool IsSuccess => Problem == PaymentCheckoutProblem.None && Value is not null;

    public static PaymentCheckoutResult Ok(PaymentCheckout value) => new(PaymentCheckoutProblem.None, value);

    public static PaymentCheckoutResult Fail(PaymentCheckoutProblem problem, string? detail = null) =>
        new(problem, null, detail);
}

public sealed record StorePaymentMethod(string Key, string DisplayName);

/// <summary>
/// Everything this application does about taking money for an order.
/// <para>
/// It deliberately owns no payment logic of its own: PaymentService decides what a payment is
/// worth, whether it succeeded and how to talk to a gateway. What lives here is the part that is
/// about <em>orders</em> — when one is payable, how a customer resumes an abandoned checkout, and
/// what a captured payment means for the order's status.
/// </para>
/// </summary>
public interface IOrderPaymentService
{
    /// <summary>Methods this store currently offers, in the order the storefront should show them.</summary>
    Task<IReadOnlyList<StorePaymentMethod>> MethodsAsync(Store store, CancellationToken ct = default);

    /// <summary>
    /// Starts or resumes payment for an order and says where to send the customer. Safe to call
    /// twice: a customer who reloads mid-redirect is handed the payment they already started.
    /// </summary>
    Task<PaymentCheckoutResult> StartAsync(Store store, Order order, string? method, string origin, CancellationToken ct = default);

    /// <summary>
    /// Re-reads the payment from the gateway and folds the answer into the order.
    /// <para>
    /// This is what a storefront calls when a customer comes back from a hosted checkout. The query
    /// string they return with was written by their own browser and proves nothing.
    /// </para>
    /// </summary>
    Task<PaymentCheckoutResult> RefreshAsync(Store store, Order order, CancellationToken ct = default);

    /// <summary>
    /// Applies a status callback pushed by PaymentService. Returns false when the callback belongs
    /// to no order here, which is answered with a 200 all the same — there is nothing to retry.
    /// </summary>
    Task<bool> ApplyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default);
}

public sealed class OrderPaymentService : IOrderPaymentService
{
    /// <summary>Statuses PaymentService reports for a payment that is finished and dead.</summary>
    private static readonly string[] DeadStatuses = ["Failed", "Cancelled", "Expired"];

    private const string Captured = "Captured";

    private readonly AppDbContext _db;
    private readonly IPaymentClient _payments;
    private readonly StorefrontOptions _storefront;
    private readonly PaymentServiceOptions _options;
    private readonly IOrderNotifier _notifier;
    private readonly ILogger<OrderPaymentService> _log;

    public OrderPaymentService(
        AppDbContext db,
        IPaymentClient payments,
        IOptions<StorefrontOptions> storefront,
        IOptions<PaymentServiceOptions> options,
        IOrderNotifier notifier,
        ILogger<OrderPaymentService> log)
    {
        _db = db;
        _payments = payments;
        _storefront = storefront.Value;
        _options = options.Value;
        _notifier = notifier;
        _log = log;
    }

    public async Task<IReadOnlyList<StorePaymentMethod>> MethodsAsync(Store store, CancellationToken ct = default)
    {
        if (!store.PaymentsEnabled || !_payments.IsConfigured) return [];

        var accounts = await _payments.ListAccountsAsync(store.Id, ct);
        if (!accounts.IsSuccess) return [];

        var enabled = accounts.Value!.Where(a => a.IsEnabled).OrderBy(a => a.Priority).ToList();
        if (enabled.Count == 0) return [];

        // The account's display name is the merchant's ("Bean Scene - Paynet - Idram"); a customer
        // wants the method's. Falling back to the account name keeps this working if the gateway
        // listing is unavailable.
        var gateways = await _payments.ListGatewaysAsync(ct);
        var names = gateways.IsSuccess
            ? gateways.Value!.ToDictionary(g => g.Key, g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            : [];

        return enabled
            .Select(a => new StorePaymentMethod(
                a.GatewayKey,
                names.TryGetValue(a.GatewayKey, out var name) ? name : a.DisplayName))
            .ToList();
    }

    public async Task<PaymentCheckoutResult> StartAsync(
        Store store, Order order, string? method, string origin, CancellationToken ct = default)
    {
        if (!_payments.IsConfigured || !order.PaymentRequired)
            return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.PaymentsUnavailable);

        if (order.Status == OrderStatus.AwaitingQuote)
            return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.AwaitingQuote);

        if (order.Status != OrderStatus.AwaitingPayment || order.Total is not { } total)
            return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.NotPayable);

        if (!string.IsNullOrWhiteSpace(method))
        {
            var offered = await MethodsAsync(store, ct);
            if (!offered.Any(m => string.Equals(m.Key, method, StringComparison.OrdinalIgnoreCase)))
                return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.MethodUnavailable);
        }

        // An order that already has a payment gets that payment back, not a second one. Anything
        // else and a customer who reloads the redirect page pays twice.
        if (order.PaymentId is { } existingId)
        {
            var existing = await _payments.SyncPaymentAsync(store.Id, existingId, ct);

            // Unreachable is not the same as dead: starting a fresh payment here could be the
            // second charge for an order that is already being paid.
            if (existing.Problem == PaymentClientProblem.Unavailable)
                return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.Unavailable, existing.Detail);

            if (existing.IsSuccess)
            {
                await ApplyAsync(order, existing.Value!.Reference, existing.Value!.Status, ct);

                if (order.PaidAt is not null)
                    return PaymentCheckoutResult.Ok(new PaymentCheckout(order, PaymentActionKind.None, null, null));

                if (!DeadStatuses.Contains(existing.Value!.Status) && IsRedirectable(order.PaymentCheckoutUrl))
                    return PaymentCheckoutResult.Ok(
                        new PaymentCheckout(order, PaymentActionKind.Redirect, order.PaymentCheckoutUrl, null));
            }
        }

        return await CreateAsync(store, order, method, origin, total, ct);
    }

    public async Task<PaymentCheckoutResult> RefreshAsync(Store store, Order order, CancellationToken ct = default)
    {
        if (order.PaymentId is not { } paymentId)
            return PaymentCheckoutResult.Ok(new PaymentCheckout(order, PaymentActionKind.None, null, null));

        var payment = await _payments.SyncPaymentAsync(store.Id, paymentId, ct);
        if (!payment.IsSuccess)
            return PaymentCheckoutResult.Fail(PaymentCheckoutProblem.Unavailable, payment.Detail);

        await ApplyAsync(order, payment.Value!.Reference, payment.Value!.Status, ct);
        return PaymentCheckoutResult.Ok(new PaymentCheckout(order, PaymentActionKind.None, null, null));
    }

    public async Task<bool> ApplyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
    {
        if (payload.OrderId is not { } orderId) return false;

        var order = await _db.Orders.FirstOrDefaultAsync(
            o => o.Id == orderId && o.StoreId == payload.MerchantId, ct);

        if (order is null) return false;

        if (order.PaymentId != payload.PaymentId)
        {
            // An abandoned attempt reporting in late. Anything short of a capture is stale news
            // about a payment we have already moved on from, and applying it would overwrite the
            // status of the attempt actually in progress.
            if (payload.Status != Captured) return true;

            // A capture is different: money moved, and pretending otherwise loses it. The order is
            // marked paid and the surplus payment is left for a human to refund.
            _log.LogWarning(
                "Payment {Payment} captured for order {Order}, which is on attempt {Attempt} ({Current}). " +
                "The order is paid; the superseded payment needs refunding.",
                payload.PaymentId, order.Id, order.PaymentAttempt, order.PaymentId);
        }

        await ApplyAsync(order, payload.Reference, payload.Status, ct);
        return true;
    }

    private async Task<PaymentCheckoutResult> CreateAsync(
        Store store, Order order, string? method, string origin, decimal total, CancellationToken ct)
    {
        // Only ever incremented when the previous payment is dead, so a reload reuses the same key
        // and PaymentService replays the original answer instead of charging again.
        var attempt = order.PaymentAttempt + 1;
        var orderUrl = $"{StorefrontLinks.StoreBase(_storefront.Url, store)}/orders/{order.Id}";

        var request = new CreatePaymentRequest(
            MerchantId: store.Id,
            Amount: total,
            Currency: store.Currency,
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            Description: $"Order {order.OrderNumber}",
            GatewayKey: method,
            CaptureMode: null,
            Customer: new PaymentCustomerRequest(order.CustomerId, order.ContactName, null, order.ContactPhone),
            // `paid=1` only tells the page to go and check; it is never taken as proof.
            ReturnUrl: $"{orderUrl}?paid=1",
            CancelUrl: orderUrl,
            CallbackUrl: $"{CallbackOrigin(origin)}/api/payments/callback",
            Metadata: new Dictionary<string, string> { ["orderNumber"] = order.OrderNumber });

        var created = await _payments.CreatePaymentAsync(request, $"order-{order.Id}-{attempt}", ct);

        if (!created.IsSuccess)
            return PaymentCheckoutResult.Fail(created.Problem switch
            {
                PaymentClientProblem.Declined => PaymentCheckoutProblem.Declined,
                PaymentClientProblem.MerchantNotConfigured or PaymentClientProblem.NotConfigured =>
                    PaymentCheckoutProblem.PaymentsUnavailable,
                _ => PaymentCheckoutProblem.Unavailable
            }, created.Detail);

        var payment = created.Value!.Payment;
        var action = created.Value!.Action;

        order.PaymentAttempt = attempt;
        order.PaymentId = payment.Id;
        order.PaymentReference = payment.Reference;
        order.PaymentStatus = payment.Status;
        order.PaymentCheckoutUrl = action.Url;
        order.UpdatedAt = DateTime.UtcNow;

        // A relative URL means the gateway does not know its own public origin. The customer would
        // be sent to the storefront's host instead and land on a 404, so it is worth saying loudly.
        if (created.Value!.Action.Kind == "Redirect" && !IsRedirectable(action.Url))
            _log.LogError(
                "Gateway {Gateway} returned a redirect to '{Url}', which is not an absolute http(s) URL. " +
                "Check the payment service's PublicBaseUrl.", payment.GatewayKey, action.Url);

        // A gateway that settles server-side can report the money already taken on the create call.
        if (payment.Status == Captured) await MarkPaidAsync(order, ct);

        await _db.SaveChangesAsync(ct);

        return PaymentCheckoutResult.Ok(new PaymentCheckout(
            order, ParseKind(action.Kind), action.Url, action.ExpiresAt));
    }

    /// <summary>
    /// Folds a payment's reported state into the order. The only transition that matters is the
    /// one into paid, and it happens once — a later delivery of the same news must not move an
    /// order the store has since progressed.
    /// </summary>
    private async Task ApplyAsync(Order order, string reference, string status, CancellationToken ct)
    {
        var changed = order.PaymentStatus != status;

        order.PaymentStatus = status;
        order.PaymentReference ??= reference;

        if (status == Captured && order.PaidAt is null)
        {
            await MarkPaidAsync(order, ct);
            changed = true;
        }

        if (!changed) return;

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Whether a stored checkout URL can still be handed to a browser. Only an absolute http(s) URL
    /// can: a relative one resolves against the storefront's own origin, so the customer would land
    /// on the shop's 404 rather than the payment page. An unusable one is treated as no URL at all,
    /// and a fresh payment is started instead of resuming a dead end.
    /// </summary>
    private static bool IsRedirectable(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task MarkPaidAsync(Order order, CancellationToken ct)
    {
        order.PaidAt = DateTime.UtcNow;

        // Paid orders join the ordinary queue: the store still has to acknowledge and fulfil them.
        // Guarded so a late notification cannot drag a Confirmed or Ready order back to New.
        if (order.Status == OrderStatus.AwaitingPayment) order.Status = OrderStatus.New;

        await _notifier.EnqueueAsync(order, OrderNotificationEvent.OrderPaid, ct);
    }

    /// <summary>
    /// Where PaymentService should post its callbacks. Configured wins; otherwise the origin of the
    /// request we are serving, which is right in development and behind a proxy that forwards the
    /// host and scheme.
    /// </summary>
    private string CallbackOrigin(string requestOrigin) =>
        string.IsNullOrWhiteSpace(_options.CallbackBaseUrl)
            ? requestOrigin.TrimEnd('/')
            : _options.CallbackBaseUrl.TrimEnd('/');

    private PaymentActionKind ParseKind(string kind)
    {
        if (Enum.TryParse<PaymentActionKind>(kind, ignoreCase: true, out var parsed)) return parsed;

        _log.LogWarning("PaymentService returned an unrecognised customer action '{Kind}'.", kind);
        return PaymentActionKind.None;
    }
}
