using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Abstractions.Gateways;
using PaymentService.Application.Notifications;
using PaymentService.Application.Payments;
using PaymentService.Application.Webhooks;
using PaymentService.Domain.Common;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Notifications;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Reconciliation;

/// <summary>
/// Closes the gap left by everything that can go wrong asynchronously.
/// <para>
/// Webhooks get lost, gateways have outages, and a create call whose answer never arrived leaves a
/// payment we cannot classify. Without something that goes back and asks, those payments stay
/// <c>Pending</c> forever and a merchant is left looking at an order that may or may not have been
/// paid for. This is the part of a payment integration that is easiest to leave out and hardest to
/// do without.
/// </para>
/// </summary>
public sealed class PaymentReconciler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PaymentServiceOptions _options;
    private readonly ILogger<PaymentReconciler> _log;

    public PaymentReconciler(
        IServiceScopeFactory scopes,
        IOptions<PaymentServiceOptions> options,
        ILogger<PaymentReconciler> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Reconciler;
        if (!settings.Enabled)
        {
            _log.LogInformation("Payment reconciler is disabled; in-flight payments will not be resolved automatically.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, settings.PollSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Reconciliation cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IPaymentOrchestrator>();
        var ingestor = scope.ServiceProvider.GetRequiredService<IWebhookIngestor>();
        var registry = scope.ServiceProvider.GetRequiredService<IPaymentGatewayRegistry>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxEnqueuer>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Stored events first: one of them may be the answer for a payment we are about to poll
        // for, and applying it is cheaper than a round trip to the gateway.
        var replayed = await ingestor.RetryFailedAsync(_options.Reconciler.BatchSize, ct);
        if (replayed > 0) _log.LogInformation("Replayed {Count} stored gateway event(s).", replayed);

        var settings = _options.Reconciler;
        var now = clock.UtcNow;
        var staleBefore = now.AddMinutes(-Math.Max(1, settings.StalePaymentMinutes));
        var abandonBefore = now.AddHours(-Math.Max(1, settings.AbandonAfterHours));

        var inFlight = new[] { PaymentStatus.Pending, PaymentStatus.RequiresAction, PaymentStatus.Processing };

        var stale = await db.Payments
            .Where(p => inFlight.Contains(p.Status) && p.UpdatedAt < staleBefore)
            .OrderBy(p => p.UpdatedAt)
            .Take(Math.Clamp(settings.BatchSize, 1, 200))
            .ToListAsync(ct);

        foreach (var payment in stale)
        {
            // Never registered and long past hope: nobody completed this checkout, and no gateway
            // holds anything for it. Safe to close, and doing so keeps a merchant's list readable.
            var neverRegistered = payment.GatewayPaymentId is null;
            var expiredByGateway = payment.ExpiresAt is { } expiry && expiry < now;

            if ((neverRegistered && payment.CreatedAt < abandonBefore) || (expiredByGateway && neverRegistered))
            {
                if (!payment.Status.CanTransitionTo(PaymentStatus.Expired)) continue;

                payment.Status = PaymentStatus.Expired;
                payment.UpdatedAt = now;
                outbox.EnqueuePayment(payment, PaymentEventTypes.Expired, now);
                await db.SaveChangesAsync(ct);
                continue;
            }

            if (neverRegistered) continue;

            // Only ask gateways that can be asked. A push-only gateway is left to its webhooks and
            // to the operator, because guessing at a payment's state would be worse than waiting.
            if (!registry.TryGet(payment.GatewayKey, out var gateway) ||
                !gateway.Capabilities.HasFlag(GatewayCapabilities.StatusPolling))
                continue;

            var result = await orchestrator.SyncAsync(payment.MerchantId, payment.Id, ct);

            if (!result.IsSuccess)
                _log.LogWarning(
                    "Reconciling payment {PaymentId} via {Gateway} failed: {Problem} {Detail}",
                    payment.Id, payment.GatewayKey, result.Problem, result.Detail);
        }
    }
}
