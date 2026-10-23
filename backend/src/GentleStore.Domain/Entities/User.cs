using GentleStore.Domain.Enums;

namespace GentleStore.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? StoreId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    /// <summary>Private chat with the notification bot; null until the user links Telegram.</summary>
    public long? TelegramChatId { get; set; }

    /// <summary>Language the bot writes to this user in (en/hy/ru), taken from the console at link time.</summary>
    public string? TelegramLanguage { get; set; }

    /// <summary>SHA-256 of the pending one-time link secret; only the bot's /start ever sees the plaintext.</summary>
    public string? TelegramLinkTokenHash { get; set; }
    public DateTime? TelegramLinkTokenExpiresAt { get; set; }

    public Store? Store { get; set; }
}
