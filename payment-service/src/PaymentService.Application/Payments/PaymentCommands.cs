using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Payments;

/// <param name="Amount">
/// In major units, matching GentleStore's <c>Order.Total</c>. Converted to minor units once, at
/// this boundary, so no caller has to know a currency's exponent.
/// </param>
/// <param name="GatewayKey">
/// Null lets the service route by the merchant's configured priority — the normal case. Naming one
/// is for a merchant who offers the customer a choice.
/// </param>
/// <param name="CallbackUrl">
/// Where status changes are POSTed. Without it the caller has to poll, which for a redirect flow
/// means the order may be paid before the caller notices.
/// </param>
public sealed record InitiatePaymentCommand(
    Guid MerchantId,
    decimal Amount,
    string Currency,
    Guid? OrderId = null,
    string? OrderNumber = null,
    string? Description = null,
    string? GatewayKey = null,
    CaptureMode CaptureMode = CaptureMode.Automatic,
    PaymentCustomer? Customer = null,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    string? CallbackUrl = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKey = null);

public sealed record PaymentCustomer(Guid? Id = null, string? Name = null, string? Email = null, string? Phone = null);

/// <param name="Amount">Null captures the full authorized amount, which is what most merchants want.</param>
public sealed record CapturePaymentCommand(decimal? Amount = null, string? IdempotencyKey = null);

public sealed record CancelPaymentCommand(string? Reason = null, string? IdempotencyKey = null);

/// <param name="Amount">Null refunds everything still refundable.</param>
public sealed record RefundPaymentCommand(
    decimal? Amount = null,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IdempotencyKey = null);

/// <summary>Filters for the merchant-facing list. All optional; merchant scope is not.</summary>
public sealed record PaymentQuery(
    Guid MerchantId,
    Guid? OrderId = null,
    Guid? CustomerId = null,
    PaymentStatus? Status = null,
    string? GatewayKey = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// A newly created payment together with what the customer has to do next. The action is not part
/// of <see cref="Payment"/> because it is short-lived and single-use — persisting it would invite
/// a storefront to reuse a dead redirect URL.
/// </summary>
public sealed record InitiatedPayment(Payment Payment, CustomerAction Action);
