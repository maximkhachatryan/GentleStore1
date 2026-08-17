namespace PaymentService.Domain.Enums;

public enum OutboxStatus
{
    Pending = 0,
    Delivered = 1,

    /// <summary>Retries exhausted. Left in place: someone has to know a merchant missed a payment.</summary>
    Abandoned = 2
}
