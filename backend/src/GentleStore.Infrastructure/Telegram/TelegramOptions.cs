namespace GentleStore.Infrastructure.Telegram;

/// <summary>
/// The bot staff link to get order alerts. Unset switches Telegram off everywhere: nothing is
/// queued and the console hides the "Connect Telegram" button.
/// </summary>
public class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>From BotFather. A secret — it sits in every request URL, so HTTP logging is off for this client.</summary>
    public string? BotToken { get; set; }

    /// <summary>Without the "@"; used to build the t.me link.</summary>
    public string? BotUsername { get; set; }

    /// <summary>
    /// Long-poll getUpdates to receive /start. Only one process per bot may poll, so turn it off on
    /// every instance but one when scaling out.
    /// </summary>
    public bool PollingEnabled { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(BotUsername);
}
