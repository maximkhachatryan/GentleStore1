namespace PaymentService.Domain.Enums;

/// <summary>
/// Kept on the account rather than inferred from the hosting environment, so one deployment can
/// carry a merchant still testing alongside merchants taking real money.
/// </summary>
public enum GatewayMode
{
    Test = 0,
    Live = 1
}
