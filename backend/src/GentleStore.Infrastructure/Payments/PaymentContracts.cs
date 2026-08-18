namespace GentleStore.Infrastructure.Payments;

// The slice of PaymentService's API this application actually uses. Deliberately hand-written and
// partial: a generated client would drag in every field of a service that is free to grow, and the
// point of the boundary is that GentleStore knows as little about payments as possible.

/// <param name="Key">Gateway key, e.g. <c>paynet-idram</c>. What a customer picks between.</param>
public record PaymentGatewayDto(
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
    IReadOnlyList<string> SupportedCurrencies);

/// <param name="CredentialRef">
/// A handle PaymentService resolves against its own secret store. Never the API key itself — that
/// must not travel through this application, its logs or its database.
/// </param>
public record UpsertGatewayAccountRequest(
    string GatewayKey,
    string Mode,
    string DisplayName,
    string CredentialRef,
    string? WebhookSecretRef,
    bool IsEnabled,
    int Priority,
    IReadOnlyList<string> SupportedCurrencies);

public record CreatePaymentRequest(
    Guid MerchantId,
    decimal Amount,
    string Currency,
    Guid? OrderId,
    string? OrderNumber,
    string? Description,
    string? GatewayKey,
    string? CaptureMode,
    PaymentCustomerRequest? Customer,
    string? ReturnUrl,
    string? CancelUrl,
    string? CallbackUrl,
    Dictionary<string, string>? Metadata);

public record PaymentCustomerRequest(Guid? Id, string? Name, string? Email, string? Phone);

/// <param name="Kind"><c>None</c>, <c>Redirect</c>, <c>ClientSdk</c> or <c>Display</c>.</param>
public record CustomerActionDto(
    string Kind,
    string? Url,
    string? HttpMethod,
    IReadOnlyDictionary<string, string>? Parameters,
    DateTime? ExpiresAt);

/// <param name="Status">The only field worth branching on. <c>Captured</c> means the money moved.</param>
public record PaymentDto(
    Guid Id,
    string Reference,
    Guid MerchantId,
    Guid? OrderId,
    string? OrderNumber,
    string Status,
    string Currency,
    decimal Amount,
    decimal CapturedAmount,
    decimal RefundedAmount,
    string GatewayKey,
    string? FailureCode,
    string? FailureMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record InitiatedPaymentDto(PaymentDto Payment, CustomerActionDto Action);

/// <summary>The body PaymentService POSTs to a payment's callback URL.</summary>
public record PaymentCallbackPayload(
    string EventType,
    Guid PaymentId,
    string Reference,
    Guid MerchantId,
    Guid? OrderId,
    string? OrderNumber,
    string Status,
    string Currency,
    decimal Amount,
    decimal CapturedAmount,
    decimal RefundedAmount,
    string GatewayKey,
    string? FailureCode,
    string? FailureMessage,
    DateTime OccurredAt);

/// <summary>
/// Why a call to PaymentService did not produce what was asked for. Kept coarse: this application
/// can only ever do three things about it — show the customer a decline, ask them to try again, or
/// tell an admin their configuration is wrong.
/// </summary>
public enum PaymentClientProblem
{
    None = 0,

    /// <summary>No base URL configured, so there is no payment service to talk to.</summary>
    NotConfigured,

    /// <summary>The store is not set up to take payments, or its credentials do not resolve.</summary>
    MerchantNotConfigured,

    /// <summary>The issuer refused. Terminal for this attempt; a retry is a new payment.</summary>
    Declined,

    /// <summary>The request was wrong — a bad amount, an unsupported currency.</summary>
    Rejected,

    /// <summary>Unreachable, timed out, or answered 5xx. Worth trying again.</summary>
    Unavailable,

    NotFound
}

public record PaymentClientResult<T>(PaymentClientProblem Problem, T? Value, string? Code = null, string? Detail = null)
    where T : class
{
    public bool IsSuccess => Problem == PaymentClientProblem.None && Value is not null;

    public static PaymentClientResult<T> Ok(T value) => new(PaymentClientProblem.None, value);

    public static PaymentClientResult<T> Fail(PaymentClientProblem problem, string? code = null, string? detail = null) =>
        new(problem, null, code, detail);
}
