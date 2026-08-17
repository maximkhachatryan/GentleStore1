using PaymentService.Domain.Common;
using PaymentService.Domain.Enums;

namespace PaymentService.Application.Abstractions.Gateways;

// Everything crossing the gateway boundary, in one file so the shape of the port is readable at a
// glance. These types are the contract an integration is written against; nothing here refers to
// EF entities, so a gateway can be tested with no database.

/// <summary>Who we are acting as. Passed on every call rather than held on the gateway instance,
/// because one instance serves every merchant and gateways are registered as singletons.</summary>
public sealed record GatewayContext(
    Guid MerchantId,
    Guid GatewayAccountId,
    string GatewayKey,
    GatewayMode Mode,
    GatewayCredentials Credentials);

/// <param name="Id">The caller's customer id, passed through so gateways can group returning payers.</param>
public sealed record GatewayCustomer(Guid? Id, string? Name, string? Email, string? Phone);

/// <param name="IdempotencyKey">
/// Ours, not the caller's, and stable per operation: a retry of the same operation must present
/// the same key so the gateway collapses it instead of charging twice.
/// </param>
public sealed record GatewayCreatePaymentRequest(
    GatewayContext Context,
    Guid PaymentId,
    string Reference,
    Money Amount,
    CaptureMode CaptureMode,
    string? Description,
    GatewayCustomer? Customer,
    string? ReturnUrl,
    string? CancelUrl,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey);

/// <param name="Amount">May be less than the authorization where the gateway allows partial capture.</param>
public sealed record GatewayCaptureRequest(
    GatewayContext Context,
    string GatewayPaymentId,
    Money Amount,
    string IdempotencyKey);

public sealed record GatewayCancelRequest(
    GatewayContext Context,
    string GatewayPaymentId,
    string? Reason,
    string IdempotencyKey);

public sealed record GatewayRefundRequest(
    GatewayContext Context,
    string GatewayPaymentId,
    Guid RefundId,
    string Reference,
    Money Amount,
    string? Reason,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey);

public sealed record GatewayFetchRequest(GatewayContext Context, string GatewayPaymentId);

// ---- What comes back ----

public enum CustomerActionKind
{
    /// <summary>Nothing to do — the payment completed server-side.</summary>
    None = 0,

    /// <summary>Send the browser to <c>Url</c>. Hosted checkout, 3-D Secure, bank selection.</summary>
    Redirect = 1,

    /// <summary>Hand <c>Parameters</c> to the gateway's client SDK and let it finish in the page.</summary>
    ClientSdk = 2,

    /// <summary>Show <c>Parameters</c> to the customer — a QR code, or transfer instructions.</summary>
    Display = 3
}

/// <summary>
/// How the customer continues. Returned rather than stored, because most of these are short-lived,
/// single-use, and a stale one is worse than none.
/// </summary>
public sealed record CustomerAction(
    CustomerActionKind Kind,
    string? Url = null,
    string? HttpMethod = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    DateTime? ExpiresAt = null)
{
    public static readonly CustomerAction None = new(CustomerActionKind.None);
}

public sealed record GatewayPaymentMethod(PaymentMethodKind Kind, string? Brand = null, string? Last4 = null);

/// <summary>
/// The gateway's view of a payment, already mapped into this service's vocabulary. Amounts are
/// nullable because a gateway that has only accepted a payment reports no captured amount yet, and
/// null must not be read as zero — the orchestrator leaves what it already knows alone.
/// </summary>
public sealed record GatewayPaymentSnapshot(
    string GatewayPaymentId,
    PaymentStatus Status,
    Money? AuthorizedAmount = null,
    Money? CapturedAmount = null,
    Money? RefundedAmount = null,
    GatewayPaymentMethod? Method = null,
    CustomerAction? Action = null,
    string? GatewayStatusCode = null,
    string? FailureCode = null,
    string? FailureMessage = null,
    DateTime? ExpiresAt = null);

public sealed record GatewayRefundSnapshot(
    string GatewayRefundId,
    RefundStatus Status,
    Money? Amount = null,
    string? GatewayStatusCode = null,
    string? FailureCode = null,
    string? FailureMessage = null,
    DateTime? SettledAt = null);

/// <param name="Code">Normalized where possible (see <see cref="GatewayErrorCodes"/>); the gateway's own code stays in <paramref name="RawCode"/>.</param>
/// <param name="Retryable">Whether repeating the identical call could plausibly succeed.</param>
public sealed record GatewayError(
    string Code,
    string? Message = null,
    string? RawCode = null,
    bool Retryable = false);

/// <summary>
/// A small shared vocabulary so callers can branch on failures without learning each gateway's
/// codes. Anything that does not fit keeps its own code and lands here as <see cref="Unknown"/>.
/// </summary>
public static class GatewayErrorCodes
{
    public const string Declined = "declined";
    public const string InsufficientFunds = "insufficient_funds";
    public const string ExpiredCard = "expired_card";
    public const string InvalidCard = "invalid_card";
    public const string AuthenticationRequired = "authentication_required";
    public const string AuthenticationFailed = "authentication_failed";
    public const string SuspectedFraud = "suspected_fraud";
    public const string InvalidRequest = "invalid_request";
    public const string CredentialsRejected = "credentials_rejected";
    public const string RateLimited = "rate_limited";
    public const string Unavailable = "gateway_unavailable";
    public const string Timeout = "timeout";
    public const string NotSupported = "not_supported";
    public const string Unknown = "unknown";
}

/// <summary>
/// The outcome of one gateway call. Integrations never throw for a declined or unavailable
/// gateway — those are expected results that must be recorded on the attempt, and an exception
/// escaping mid-call is exactly the case where it is unclear whether money moved.
/// </summary>
public sealed record GatewayResult<T>(
    AttemptOutcome Outcome,
    T? Value,
    GatewayError? Error = null,
    string? RawResponse = null)
    where T : class
{
    public bool IsSuccess => Outcome == AttemptOutcome.Succeeded && Value is not null;

    public static GatewayResult<T> Success(T value, string? raw = null) =>
        new(AttemptOutcome.Succeeded, value, null, raw);

    /// <summary>The issuer said no. A final answer about this payment, so never retried as-is.</summary>
    public static GatewayResult<T> Declined(GatewayError error, T? value = null, string? raw = null) =>
        new(AttemptOutcome.Declined, value, error, raw);

    /// <summary>The gateway refused the request itself — bad amount, bad credentials, unsupported currency.</summary>
    public static GatewayResult<T> Rejected(GatewayError error, string? raw = null) =>
        new(AttemptOutcome.Rejected, null, error, raw);

    /// <summary>Timeout, 5xx, socket error. Safe to retry with the same idempotency key.</summary>
    public static GatewayResult<T> Transient(GatewayError error, string? raw = null) =>
        new(AttemptOutcome.TransientFailure, null, error with { Retryable = true }, raw);

    public static GatewayResult<T> Permanent(GatewayError error, string? raw = null) =>
        new(AttemptOutcome.PermanentFailure, null, error, raw);

    /// <summary>
    /// The call may have succeeded — use only when the gateway was reached but the answer was lost.
    /// Resolved by fetching the payment, never by retrying the write.
    /// </summary>
    public static GatewayResult<T> Unknown(GatewayError error, string? raw = null) =>
        new(AttemptOutcome.Unknown, null, error, raw);
}
