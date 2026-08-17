using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
