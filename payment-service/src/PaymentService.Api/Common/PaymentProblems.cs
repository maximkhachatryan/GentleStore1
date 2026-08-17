using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Payments;

namespace PaymentService.Api.Common;

/// <summary>
/// Turns a <see cref="PaymentProblem"/> into a status code and a stable string code, following the
/// same convention as GentleStore's public API: the caller switches on the code, never on prose.
/// </summary>
public static class PaymentProblems
{
    public static IActionResult ToActionResult<T>(this PaymentResult<T> result, ControllerBase controller)
        where T : class
    {
        var (status, code) = Map(result.Problem);

        return controller.StatusCode(status, new
        {
            code,
            detail = result.Detail,
            gatewayCode = result.GatewayError?.RawCode,
            retryable = result.GatewayError?.Retryable ?? false
        });
    }

    private static (int Status, string Code) Map(PaymentProblem problem) => problem switch
    {
        PaymentProblem.InvalidAmount => (StatusCodes.Status400BadRequest, "invalid_amount"),
        PaymentProblem.InvalidCurrency => (StatusCodes.Status400BadRequest, "invalid_currency"),
        PaymentProblem.InvalidReturnUrl => (StatusCodes.Status400BadRequest, "invalid_url"),
        PaymentProblem.MetadataTooLarge => (StatusCodes.Status400BadRequest, "metadata_too_large"),
        PaymentProblem.UnknownGateway => (StatusCodes.Status400BadRequest, "gateway_unknown"),
        PaymentProblem.UnsupportedCurrency => (StatusCodes.Status400BadRequest, "currency_unsupported"),

        PaymentProblem.PaymentNotFound => (StatusCodes.Status404NotFound, "payment_not_found"),
        PaymentProblem.RefundNotFound => (StatusCodes.Status404NotFound, "refund_not_found"),

        // The request is well formed; the merchant is not set up to take it. Separated from a 400
        // because the caller cannot fix it by changing the request.
        PaymentProblem.NoGatewayConfigured => (StatusCodes.Status422UnprocessableEntity, "gateway_not_configured"),
        PaymentProblem.GatewayDisabled => (StatusCodes.Status422UnprocessableEntity, "gateway_disabled"),
        PaymentProblem.GatewayMisconfigured => (StatusCodes.Status422UnprocessableEntity, "gateway_misconfigured"),

        PaymentProblem.InvalidState => (StatusCodes.Status409Conflict, "invalid_state"),
        PaymentProblem.CapabilityNotSupported => (StatusCodes.Status409Conflict, "not_supported"),
        PaymentProblem.CaptureExceedsAuthorized => (StatusCodes.Status409Conflict, "capture_exceeds_authorized"),
        PaymentProblem.RefundExceedsCaptured => (StatusCodes.Status409Conflict, "refund_exceeds_captured"),
        PaymentProblem.IdempotencyConflict => (StatusCodes.Status409Conflict, "idempotency_conflict"),

        // 402 is the one status that means exactly this: the request was understood and the money
        // was refused. Distinct from a 502, so a caller can tell "the customer's card failed" from
        // "the gateway is broken" without reading the body.
        PaymentProblem.GatewayDeclined => (StatusCodes.Status402PaymentRequired, "declined"),

        PaymentProblem.GatewayRejected => (StatusCodes.Status502BadGateway, "gateway_rejected"),
        PaymentProblem.GatewayUnavailable => (StatusCodes.Status503ServiceUnavailable, "gateway_unavailable"),

        // Not a failure: we do not know. The payment stays in flight and the reconciler resolves
        // it, so the caller should re-read rather than treat this as "not paid".
        PaymentProblem.GatewayOutcomeUnknown => (StatusCodes.Status504GatewayTimeout, "gateway_outcome_unknown"),

        _ => (StatusCodes.Status500InternalServerError, "unexpected")
    };
}
