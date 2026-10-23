using GentleStore.Api.Storefront;
using GentleStore.Infrastructure.Persistence;
using GentleStore.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GentleStore.Api.Notifications;

/// <summary>
/// Long-polls the bot for <c>/start &lt;secret&gt;</c> (link a console user to this chat) and
/// <c>/stop</c> (unlink). Polling needs no public URL, but only one process per bot may do it —
/// see <see cref="TelegramOptions.PollingEnabled"/>.
/// </summary>
public sealed class TelegramLinkPoller : BackgroundService
{
    private const int LongPollSeconds = 25;
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramLinkPoller> _log;

    public TelegramLinkPoller(IServiceScopeFactory scopes, IOptions<TelegramOptions> options, ILogger<TelegramLinkPoller> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured || !_options.PollingEnabled) return;

        long offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var telegram = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var updates = await telegram.GetUpdatesAsync(offset, LongPollSeconds, stoppingToken);
                if (updates is null)
                {
                    await Task.Delay(ErrorDelay, stoppingToken);
                    continue;
                }

                foreach (var update in updates)
                {
                    offset = Math.Max(offset, update.UpdateId + 1);
                    try
                    {
                        await HandleAsync(db, telegram, update, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _log.LogError(ex, "Failed to handle Telegram update {Update}.", update.UpdateId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Telegram polling cycle failed.");
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }

    private static async Task HandleAsync(AppDbContext db, ITelegramClient telegram, TelegramUpdate update, CancellationToken ct)
    {
        // Group chats are refused: an alert carries a customer's phone, and a group's membership is
        // outside the store's control.
        if (update.Message is not { Text: { } text } message || message.Chat.Type != "private") return;

        var chatId = message.Chat.Id;
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;
        var command = parts[0].Split('@')[0].ToLowerInvariant();

        switch (command)
        {
            case "/start":
            {
                var secret = parts.Length > 1 ? parts[1] : null;
                if (!StorefrontTokens.LooksLikeSecret(secret))
                {
                    await telegram.SendMessageAsync(chatId, OrderNotificationTexts.LinkInvalid(), ct);
                    return;
                }

                var hash = StorefrontTokens.Hash(secret!);
                var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramLinkTokenHash == hash, ct);

                if (user is null || !user.IsActive || user.TelegramLinkTokenExpiresAt < DateTime.UtcNow)
                {
                    await telegram.SendMessageAsync(chatId, OrderNotificationTexts.LinkInvalid(), ct);
                    return;
                }

                user.TelegramChatId = chatId;
                user.TelegramLinkTokenHash = null;
                user.TelegramLinkTokenExpiresAt = null;
                await db.SaveChangesAsync(ct);

                await telegram.SendMessageAsync(chatId, OrderNotificationTexts.Linked(user.TelegramLanguage), ct);
                return;
            }

            case "/stop":
            {
                var users = await db.Users.Where(u => u.TelegramChatId == chatId).ToListAsync(ct);
                if (users.Count == 0) return;

                foreach (var user in users) user.TelegramChatId = null;
                await db.SaveChangesAsync(ct);

                await telegram.SendMessageAsync(chatId, OrderNotificationTexts.Unlinked(users[0].TelegramLanguage), ct);
                return;
            }
        }
    }
}
