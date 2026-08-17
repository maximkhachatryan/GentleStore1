using PaymentService.Application.Abstractions.Gateways;

namespace PaymentService.Application.Payments;

/// <summary>
/// Success or a reason, without exceptions for outcomes that are entirely expected. A decline is
/// not exceptional — it is most of what a payment service does.
/// </summary>
public sealed record PaymentResult<T>(
    PaymentProblem Problem,
    T? Value,
    string? Detail = null,
    GatewayError? GatewayError = null)
    where T : class
{
    public bool IsSuccess => Problem == PaymentProblem.None && Value is not null;

    public static PaymentResult<T> Ok(T value) => new(PaymentProblem.None, value);

    public static PaymentResult<T> Fail(PaymentProblem problem, string? detail = null, GatewayError? error = null) =>
        new(problem, null, detail, error);
}
