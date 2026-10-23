using GentleStore.Infrastructure.Auth;
using GentleStore.Infrastructure.Payments;
using GentleStore.Infrastructure.Persistence;
using GentleStore.Infrastructure.Telegram;
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
        AddTelegram(services, configuration);

        return services;
    }

    private static void AddTelegram(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

        services.AddHttpClient(TelegramClient.HttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
                // Above the 25 s getUpdates long poll, so an idle poll ends on Telegram's side, not ours.
                client.Timeout = TimeSpan.FromSeconds(40);
            })
            // The bot token is part of every request path; the default handlers would log it.
            .RemoveAllLoggers();

        services.AddScoped<ITelegramClient, TelegramClient>();
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
