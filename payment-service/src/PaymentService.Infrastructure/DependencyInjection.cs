using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Merchants;
using PaymentService.Application.Notifications;
using PaymentService.Application.Payments;
using PaymentService.Application.Webhooks;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Gateways;
using PaymentService.Infrastructure.Gateways.Paynet;
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
        AddPaynet(services, configuration);

        // A test double, not a provider. Off unless explicitly switched on, and never in production.
        if (configuration.GetValue<bool>("Gateways:sandbox:Enabled"))
        {
            services.AddSingleton<SandboxPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<SandboxPaymentGateway>());
        }
    }

    /// <summary>
    /// Registers one gateway per Paynet processor — <c>paynet-arca</c>, <c>paynet-idram</c>,
    /// <c>paynet-telcell</c> — all sharing a merchant's single API key.
    /// <para>
    /// To a customer these are different ways to pay, and a shop offers several at once. Making
    /// each a gateway key means the storefront chooses one exactly as it would choose between two
    /// providers, and the routing, capability and account machinery all work unchanged.
    /// </para>
    /// </summary>
    private static void AddPaynet(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PaynetOptions.SectionName);
        services.Configure<PaynetOptions>(section);

        var options = section.Get<PaynetOptions>() ?? new PaynetOptions();
        if (!options.Enabled) return;

        var baseUrl = options.BaseUrl.TrimEnd('/') + "/";

        services.AddHttpClient(PaynetGateway.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        });

        var processors = options.Processors
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(p => p.Length > 0)
            .Distinct()
            .ToList();

        foreach (var processor in processors)
        {
            // Captured per iteration, so each registration gets its own slug rather than the last
            // one in the list.
            var slug = processor;

            services.AddSingleton<IPaymentGateway>(provider => new PaynetGateway(
                slug,
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<IOptions<PaymentServiceOptions>>(),
                provider.GetRequiredService<ILogger<PaynetGateway>>()));

            services.AddSingleton<IGatewayWebhookReader>(provider => new PaynetWebhookReader(
                slug, provider.GetRequiredService<ILogger<PaynetWebhookReader>>()));
        }
    }
}
