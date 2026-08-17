using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Notifications;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Notifications;

/// <summary>
/// Drains the outbox, with exponential backoff on failure.
/// <para>
/// Delivery is at-least-once, never exactly-once: a callback that succeeds and whose acknowledgement
/// is lost gets sent again. Receivers are expected to deduplicate on <c>X-Payment-Event-Id</c>, or
/// simply to treat the callback as a nudge and re-read the payment — which is idempotent anyway.
/// </para>
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PaymentServiceOptions _options;
    private readonly ILogger<OutboxDispatcher> _log;

    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        IOptions<PaymentServiceOptions> options,
        ILogger<OutboxDispatcher> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outbox = _options.Outbox;
        if (!outbox.Enabled)
        {
            _log.LogInformation("Outbox dispatcher is disabled; payment callbacks will not be delivered.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, outbox.PollSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must outlive any single failure: a dispatcher that dies on a transient
                // database error silently stops every merchant hearing about their payments.
                _log.LogError(ex, "Outbox dispatch cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IMerchantCallbackSender>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var outbox = _options.Outbox;
        var now = clock.UtcNow;

        var due = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(Math.Clamp(outbox.BatchSize, 1, 500))
            .ToListAsync(ct);

        foreach (var message in due)
        {
            // Push the next attempt out before sending. With more than one instance running this
            // is what stops both delivering the same callback: the second finds the row no longer
            // due and moves on. It also doubles as the timeout if this instance dies mid-send.
            var lease = now.AddSeconds(Math.Max(outbox.TimeoutSeconds * 2, 30));

            var claimed = await db.OutboxMessages
                .Where(m => m.Id == message.Id && m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.NextAttemptAt, lease), ct);

            if (claimed == 0) continue;

            var result = await sender.SendAsync(message, ct);
            var completedAt = clock.UtcNow;
            var attempts = message.Attempts + 1;

            if (result.Delivered)
            {
                await db.OutboxMessages
                    .Where(m => m.Id == message.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.Status, OutboxStatus.Delivered)
                        .SetProperty(m => m.Attempts, attempts)
                        .SetProperty(m => m.DeliveredAt, completedAt)
                        .SetProperty(m => m.LastError, (string?)null), ct);
                continue;
            }

            var exhausted = !result.Retryable || attempts >= outbox.MaxAttempts;
            var status = exhausted ? OutboxStatus.Abandoned : OutboxStatus.Pending;
            var nextAttempt = completedAt.Add(Backoff(attempts, outbox));
            var error = Truncate(result.Error, 2000);

            if (exhausted)
                _log.LogError(
                    "Giving up on callback {MessageId} ({EventType}) for payment {PaymentId} after {Attempts} attempts: {Error}",
                    message.Id, message.EventType, message.PaymentId, attempts, result.Error);

            await db.OutboxMessages
                .Where(m => m.Id == message.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, status)
                    .SetProperty(m => m.Attempts, attempts)
                    .SetProperty(m => m.NextAttemptAt, nextAttempt)
                    .SetProperty(m => m.LastError, error), ct);
        }
    }

    private static TimeSpan Backoff(int attempts, OutboxOptions options)
    {
        // Doubling, capped. Shifting past 30 would overflow, so the exponent is clamped first.
        var exponent = Math.Min(attempts, 30);
        var seconds = Math.Min((double)options.MaxBackoffSeconds, options.BaseBackoffSeconds * Math.Pow(2, exponent - 1));
        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
