using GentleStore.Api.Auth;
using GentleStore.Api.Contracts;
using GentleStore.Api.Notifications;
using GentleStore.Api.Storefront;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using GentleStore.Infrastructure.Telegram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Controllers;

/// <summary>
/// The signed-in user's own alert settings. Per user, not per store: each staff member links their
/// own Telegram and can switch it off without affecting colleagues.
/// </summary>
[Route("api/backoffice/notifications")]
public class BackofficeNotificationsController : BackofficeControllerBase
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITelegramClient _telegram;

    public BackofficeNotificationsController(AppDbContext db, ICurrentUser current, ITelegramClient telegram)
    {
        _db = db;
        _current = current;
        _telegram = telegram;
    }

    [HttpGet("telegram")]
    public async Task<IActionResult> GetTelegram()
    {
        // A platform admin managing a store is not one of its staff, so alerts never reach them.
        if (_current.Role == UserRole.SuperAdmin || _current.UserId is not Guid userId) return Forbidden();

        var linked = await _db.Users.AnyAsync(u => u.Id == userId && u.TelegramChatId != null);
        return Ok(new TelegramStatusDto(_telegram.IsConfigured, linked, _telegram.BotUsername));
    }

    [HttpPost("telegram/link")]
    public async Task<IActionResult> LinkTelegram(LinkTelegramRequest req)
    {
        if (_current.Role == UserRole.SuperAdmin || _current.UserId is not Guid userId) return Forbidden();
        if (!_telegram.IsConfigured)
            return Conflict(new { error = "Telegram is not configured.", code = "telegram_not_configured" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound();

        var secret = StorefrontTokens.NewSecret();
        var expiresAt = DateTime.UtcNow.Add(LinkLifetime);

        user.TelegramLinkTokenHash = StorefrontTokens.Hash(secret);
        user.TelegramLinkTokenExpiresAt = expiresAt;
        user.TelegramLanguage = OrderNotificationTexts.Normalize(req.Language);
        await _db.SaveChangesAsync();

        return Ok(new TelegramLinkDto($"https://t.me/{_telegram.BotUsername}?start={secret}", expiresAt));
    }

    [HttpDelete("telegram")]
    public async Task<IActionResult> UnlinkTelegram()
    {
        if (_current.Role == UserRole.SuperAdmin || _current.UserId is not Guid userId) return Forbidden();

        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.TelegramChatId, (long?)null)
                .SetProperty(u => u.TelegramLinkTokenHash, (string?)null)
                .SetProperty(u => u.TelegramLinkTokenExpiresAt, (DateTime?)null));

        return NoContent();
    }
}
