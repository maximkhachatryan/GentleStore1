using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;

namespace PaymentService.Api.Contracts;

/// <summary>
/// Entity to DTO, in one place. The wire shape is deliberately not the storage shape: attempts,
/// idempotency keys, credential references and raw gateway payloads all stay inside the service.
/// </summary>
public static class PaymentMappings
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.Reference,
        payment.MerchantId,
        payment.OrderId,
        payment.OrderNumber,
        payment.Status.ToString(),
        payment.CaptureMode.ToString(),
        payment.Currency,
        payment.Amount.ToDecimal(),
        payment.AmountMinor,
        payment.Authorized.ToDecimal(),
        payment.Captured.ToDecimal(),
        payment.Refunded.ToDecimal(),
        payment.RefundableRemaining.ToDecimal(),
        payment.GatewayKey,
        payment.GatewayPaymentId,
        payment.Description,
        payment.MethodKind == PaymentMethodKind.Unknown ? null : payment.MethodKind.ToString(),
        payment.MethodBrand,
        payment.MethodLast4,
        payment.FailureCode,
        payment.FailureMessage,
        payment.ReturnUrl,
        payment.Metadata,
        payment.CreatedAt,
        payment.UpdatedAt,
        payment.AuthorizedAt,
        payment.CapturedAt,
        payment.CancelledAt,
        payment.ExpiresAt,
        payment.Refunds.OrderBy(r => r.CreatedAt).Select(ToDto).ToList());

    public static RefundDto ToDto(this Refund refund) => new(
        refund.Id,
        refund.Reference,
        refund.Status.ToString(),
        refund.Currency,
        refund.Amount.ToDecimal(),
        refund.AmountMinor,
        refund.Reason,
        refund.FailureCode,
        refund.FailureMessage,
        refund.CreatedAt,
        refund.SettledAt);

    public static CustomerActionDto ToDto(this CustomerAction action) => new(
        action.Kind.ToString(),
        action.Url,
        action.HttpMethod,
        action.Parameters,
        action.ExpiresAt);

    public static GatewayDto ToDto(this IPaymentGateway gateway) => new(
        gateway.Key,
        gateway.DisplayName,
        Enum.GetValues<GatewayCapabilities>()
            .Where(flag => flag != GatewayCapabilities.None && gateway.Capabilities.HasFlag(flag))
            .Select(flag => flag.ToString())
            .ToList(),
        gateway.SupportedCurrencies.ToList(),
        gateway.Capabilities.HasFlag(GatewayCapabilities.Webhooks));

    public static GatewayAccountDto ToDto(this MerchantGatewayAccount account) => new(
        account.Id,
        account.MerchantId,
        account.GatewayKey,
        account.Mode.ToString(),
        account.DisplayName,
        account.CredentialRef,
        account.IsEnabled,
        account.Priority,
        account.SupportedCurrencies,
        account.CreatedAt,
        account.UpdatedAt);
}
