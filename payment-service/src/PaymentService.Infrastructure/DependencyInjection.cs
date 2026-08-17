using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Merchants;
using PaymentService.Application.Notifications;
using PaymentService.Application.Payments;
using PaymentService.Application.Webhooks;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Gateways;
using PaymentService.Infrastructure.Gateways.Sandbox;
using PaymentService.Infrastructure.Merchants;
using PaymentService.Infrastructure.Notifications;
using PaymentService.Infrastructure.Payments;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Reconciliation;
using PaymentService.Infrastructure.Webhooks;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' was not found.");

        services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connectionString));

        services.Configure<PaymentServiceOptions>(configuration.GetSection(PaymentServiceOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IReferenceGenerator, ReferenceGenerator>();

        // Singleton: the registry closes over the gateway instances, which are themselves
        // stateless singletons, and rebuilding it per request would validate the same wiring on
        // every call.
        services.AddSingleton<IPaymentGatewayRegistry, PaymentGatewayRegistry>();
        services.AddSingleton<IGatewayCredentialProvider, ConfigurationCredentialProvider>();

        services.AddScoped<IGatewayRouter, GatewayRouter>();
        services.AddScoped<IPaymentStateApplier, PaymentStateApplier>();
        services.AddScoped<IOutboxEnqueuer, OutboxEnqueuer>();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IWebhookIngestor, WebhookIngestor>();
        services.AddScoped<IMerchantGatewayAccountService, MerchantGatewayAccountService>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        services.AddScoped<IMerchantCallbackSender, HttpMerchantCallbackSender>();
        services.AddHttpClient(HttpMerchantCallbackSender.HttpClientName, (provider, client) =>
        {
            var options = configuration.GetSection(PaymentServiceOptions.SectionName)
                .Get<PaymentServiceOptions>() ?? new PaymentServiceOptions();

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Outbox.TimeoutSeconds));
        });

        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<PaymentReconciler>();

        AddGateways(services, configuration);

        return services;
    }

    /// <summary>
    /// Where payment gateway integrations are registered.
    /// <para>
    /// Adding one is a single line — <c>services.AddSingleton&lt;IPaymentGateway, StripeGateway&gt;()</c>,
    /// plus <c>AddSingleton&lt;IGatewayWebhookReader, StripeWebhookReader&gt;()</c> if it pushes
    /// notifications. Nothing else in the service changes: routing, capability checks, attempts,
    /// the status rules and the outbox are all already in place and gateway-agnostic.
    /// </para>
    /// </summary>
    private static void AddGateways(IServiceCollection services, IConfiguration configuration)
    {
        // No real integrations yet. The sandbox is a test double, not a provider, and stays off
        // unless it is explicitly switched on.
        if (configuration.GetValue<bool>("Gateways:sandbox:Enabled"))
        {
            services.AddSingleton<SandboxPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<SandboxPaymentGateway>());
        }
    }
}
