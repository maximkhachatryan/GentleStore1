using GentleStore.Infrastructure.Auth;
using GentleStore.Infrastructure.Payments;
using GentleStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GentleStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' was not found.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        AddPayments(services, configuration);

        return services;
    }

    /// <summary>
    /// Wires the client for PaymentService. Registered whether or not a base URL is configured:
    /// with none, <c>IPaymentClient.IsConfigured</c> is false and every call fails fast with
    /// <c>NotConfigured</c>, which the admin screen shows as "payments are not available in this
    /// deployment" rather than an error.
    /// </summary>
    private static void AddPayments(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PaymentServiceOptions.SectionName);
        services.Configure<PaymentServiceOptions>(section);

        var options = section.Get<PaymentServiceOptions>() ?? new PaymentServiceOptions();

        services.AddHttpClient(PaymentClient.HttpClientName, client =>
        {
            if (options.IsConfigured) client.BaseAddress = new Uri(options.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        });

        services.AddScoped<IPaymentClient, PaymentClient>();
        services.AddSingleton<IPaymentCallbackVerifier, PaymentCallbackVerifier>();
    }
}
