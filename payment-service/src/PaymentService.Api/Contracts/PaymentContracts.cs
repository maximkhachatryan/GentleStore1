namespace PaymentService.Api.Contracts;

// ---- Requests ----

/// <param name="MerchantId">GentleStore's <c>StoreId</c>. Scopes everything that follows.</param>
/// <param name="Amount">
/// In major units — 10.50, not 1050 — matching GentleStore's <c>Order.Total</c>. The response
/// carries both this and <c>amountMinor</c>, so a caller that prefers integers never has to
/// reason about a currency's exponent.
/// </param>
/// <param name="GatewayKey">Omit to let the merchant's configured priority decide.</param>
/// <param name="CaptureMode">
/// <c>Automatic</c> takes the money at checkout. <c>Manual</c> holds it and waits for
/// <c>POST /capture</c> — the right choice for a store that confirms stock by hand.
/// </param>
/// <param name="CallbackUrl">Where status changes are POSTed. Without it, the caller has to poll.</param>
public record CreatePaymentRequest(
    Guid MerchantId,
    decimal Amount,
    string Currency,
    Guid? OrderId = null,
    string? OrderNumber = null,
    string? Description = null,
    string? GatewayKey = null,
    string? CaptureMode = null,
    PaymentCustomerRequest? Customer = null,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    string? CallbackUrl = null,
    Dictionary<string, string>? Metadata = null);

public record PaymentCustomerRequest(Guid? Id = null, string? Name = null, string? Email = null, string? Phone = null);

public record CapturePaymentRequest(decimal? Amount = null);

public record CancelPaymentRequest(string? Reason = null);

public record CreateRefundRequest(decimal? Amount = null, string? Reason = null, Dictionary<string, string>? Metadata = null);

public record UpsertGatewayAccountRequest(
    string GatewayKey,
    string Mode,
    string DisplayName,
    string CredentialRef,
    string? WebhookSecretRef = null,
    bool IsEnabled = true,
    int Priority = 0,
    List<string>? SupportedCurrencies = null);

// ---- Responses ----

/// <param name="Kind">
/// <c>None</c>, <c>Redirect</c>, <c>ClientSdk</c> or <c>Display</c>. Anything other than
/// <c>None</c> means the payment is not finished until the customer has done something.
/// </param>
public record CustomerActionDto(
    string Kind,
    string? Url,
    string? HttpMethod,
    IReadOnlyDictionary<string, string>? Parameters,
    DateTime? ExpiresAt);

public record RefundDto(
    Guid Id, string Reference, string Status, string Currency,
    decimal Amount, long AmountMinor, string? Reason,
    string? FailureCode, string? FailureMessage,
    DateTime CreatedAt, DateTime? SettledAt);

/// <param name="Status">
/// The only field worth branching on. <c>Captured</c> means the money moved; <c>Authorized</c>
/// means it is held and needs a capture; everything else means not yet or not at all.
/// </param>
/// <param name="MethodLast4">Never a full card number — the most that is ever stored is what a receipt may show.</param>
public record PaymentDto(
    Guid Id,
    string Reference,
    Guid MerchantId,
    Guid? OrderId,
    string? OrderNumber,
    string Status,
    string CaptureMode,
    string Currency,
    decimal Amount,
    long AmountMinor,
    decimal AuthorizedAmount,
    decimal CapturedAmount,
    decimal RefundedAmount,
    decimal RefundableAmount,
    string GatewayKey,
    string? GatewayPaymentId,
    string? Description,
    string? MethodKind,
    string? MethodBrand,
    string? MethodLast4,
    string? FailureCode,
    string? FailureMessage,
    string? ReturnUrl,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? AuthorizedAt,
    DateTime? CapturedAt,
    DateTime? CancelledAt,
    DateTime? ExpiresAt,
    IReadOnlyList<RefundDto> Refunds);

/// <summary>What a create returns: the payment, plus where to send the customer next.</summary>
public record InitiatedPaymentDto(PaymentDto Payment, CustomerActionDto Action);

public record PagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record GatewayDto(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> SupportedCurrencies,
    bool SupportsWebhooks);

public record GatewayAccountDto(
    Guid Id,
    Guid MerchantId,
    string GatewayKey,
    string Mode,
    string DisplayName,
    string CredentialRef,
    bool IsEnabled,
    int Priority,
    IReadOnlyList<string> SupportedCurrencies,
    DateTime CreatedAt,
    DateTime UpdatedAt);
