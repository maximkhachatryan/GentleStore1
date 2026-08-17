namespace PaymentService.Domain.Enums;

/// <summary>
/// Coarse enough that a new gateway rarely needs a new member, which matters because this value is
/// persisted and shown to merchants. Anything unrecognised maps to <see cref="Other"/> and keeps
/// its precise name in <c>Payment.MethodBrand</c>.
/// </summary>
public enum PaymentMethodKind
{
    Unknown = 0,
    Card = 1,
    BankTransfer = 2,
    DirectDebit = 3,
    Wallet = 4,
    BuyNowPayLater = 5,
    Cash = 6,
    Crypto = 7,
    Other = 99
}
