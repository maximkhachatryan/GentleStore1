using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using GentleStore.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Notifications;

/// <summary>
/// Drains <c>OrderNotifications</c> to Telegram with exponential backoff. At-least-once: a send
/// whose acknowledgement is lost is repeated, which for a staff alert is harmless.
/// </summary>
public sealed class OrderNotificationDispatcher : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 8;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly TelegramOptions _telegram;
    private readonly ConsoleAppOptions _console;
    private readonly ILogger<OrderNotificationDispatcher> _log;

    public OrderNotificationDispatcher(
        IServiceScopeFactory scopes,
        IOptions<TelegramOptions> telegram,
        IOptions<ConsoleAppOptions> console,
        ILogger<OrderNotificationDispatcher> log)
    {
        _scopes = scopes;
        _telegram = telegram.Value;
        _console = console.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_telegram.IsConfigured)
        {
            _log.LogInformation("Telegram is not configured; order notifications are off.");
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);

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
                _log.LogError(ex, "Order notification dispatch cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramClient>();

        var now = DateTime.UtcNow;

        var due = await db.OrderNotifications
            .Where(n => n.Status == OrderNotificationStatus.Pending && n.NextAttemptAt <= now)
            .OrderBy(n => n.NextAttemptAt)
            .Take(BatchSize)
            .Select(n => n.Id)
            .ToListAsync(ct);

        foreach (var id in due)
        {
            // Pushing the next attempt out first is the claim: a second instance finds the row no
            // longer due, and if this one dies mid-send the row comes back after the lease.
            var claimed = await db.OrderNotifications
                .Where(n => n.Id == id && n.Status == OrderNotificationStatus.Pending && n.NextAttemptAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.NextAttemptAt, now.Add(Lease)), ct);

            if (claimed == 0) continue;

            var notification = await db.OrderNotifications
                .AsNoTracking()
                .Include(n => n.User)
                .Include(n => n.Order).ThenInclude(o => o!.Lines)
                .FirstAsync(n => n.Id == id, ct);

            var user = notification.User!;
            var order = notification.Order!;

            if (user.TelegramChatId is not { } chatId || !user.IsActive || user.StoreId != order.StoreId)
            {
                await FinishAsync(db, id, OrderNotificationStatus.Abandoned, notification.Attempts, "recipient_unlinked", ct);
                continue;
            }

            var text = OrderNotificationTexts.Render(notification.Event, order, user.TelegramLanguage, _console.Url);
            var result = await telegram.SendMessageAsync(chatId, text, ct);
            var attempts = notification.Attempts + 1;

            if (result.Delivered)
            {
                await db.OrderNotifications
                    .Where(n => n.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(n => n.Status, OrderNotificationStatus.Sent)
                        .SetProperty(n => n.Attempts, attempts)
                        .SetProperty(n => n.SentAt, DateTime.UtcNow)
                        .SetProperty(n => n.LastError, (string?)null), ct);
                continue;
            }

            if (result.ChatGone)
            {
                // Blocked the bot or deleted the chat: unlink, so the console shows "not connected"
                // and nothing else is queued for a chat that will never read it.
                await db.Users
                    .Where(u => u.Id == user.Id && u.TelegramChatId == chatId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.TelegramChatId, (long?)null), ct);
                _log.LogInformation("Telegram chat for user {User} is gone ({Error}); unlinked.", user.Id, result.Error);
            }

            if (!result.Retryable || attempts >= MaxAttempts)
            {
                if (!result.ChatGone)
                    _log.LogError("Giving up on order notification {Id} for order {Order} after {Attempts} attempts: {Error}",
                        id, order.Id, attempts, result.Error);
                await FinishAsync(db, id, OrderNotificationStatus.Abandoned, attempts, result.Error, ct);
                continue;
            }

            var delay = Backoff(attempts);
            if (result.RetryAfter is { } retryAfter && retryAfter > delay) delay = retryAfter;

            await db.OrderNotifications
                .Where(n => n.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.Attempts, attempts)
                    .SetProperty(n => n.NextAttemptAt, DateTime.UtcNow.Add(delay))
                    .SetProperty(n => n.LastError, Truncate(result.Error)), ct);
        }
    }

    private static Task FinishAsync(
        AppDbContext db, Guid id, OrderNotificationStatus status, int attempts, string? error, CancellationToken ct) =>
        db.OrderNotifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status, status)
                .SetProperty(n => n.Attempts, attempts)
                .SetProperty(n => n.LastError, Truncate(error)), ct);

    private static TimeSpan Backoff(int attempts)
    {
        var seconds = BaseBackoff.TotalSeconds * Math.Pow(2, Math.Min(attempts, 20) - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }

    private static string? Truncate(string? value) =>
        value is null || value.Length <= 2000 ? value : value[..2000];
}
