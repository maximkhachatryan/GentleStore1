using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only; the running service gets its options from DI.</summary>
public class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PAYMENTS_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=gentlestore_payments;Username=gentlestore;Password=gentlestore_dev_pw";

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PaymentsDbContext(options);
    }
}
