using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GentleStore.Infrastructure.Telegram;

public interface ITelegramClient
{
    bool IsConfigured { get; }
    string? BotUsername { get; }

    Task<TelegramSendResult> SendMessageAsync(long chatId, string html, CancellationToken ct = default);

    /// <summary>Long poll. Returns null when the call failed; the caller backs off.</summary>
    Task<IReadOnlyList<TelegramUpdate>?> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default);
}

/// <param name="ChatGone">The user blocked the bot or the chat no longer exists — stop writing to it.</param>
public sealed record TelegramSendResult(bool Delivered, bool Retryable, bool ChatGone, TimeSpan? RetryAfter, string? Error)
{
    public static readonly TelegramSendResult Ok = new(true, false, false, null, null);
}

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type);

public sealed class TelegramClient : ITelegramClient
{
    public const string HttpClientName = "telegram";

    private readonly IHttpClientFactory _clients;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramClient> _log;

    public TelegramClient(IHttpClientFactory clients, IOptions<TelegramOptions> options, ILogger<TelegramClient> log)
    {
        _clients = clients;
        _options = options.Value;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;
    public string? BotUsername => _options.BotUsername?.TrimStart('@');

    public async Task<TelegramSendResult> SendMessageAsync(long chatId, string html, CancellationToken ct = default)
    {
        if (!IsConfigured) return new(false, false, false, null, "telegram_not_configured");

        var body = new
        {
            chat_id = chatId,
            text = html,
            parse_mode = "HTML",
            link_preview_options = new { is_disabled = true }
        };

        try
        {
            using var response = await Http().PostAsJsonAsync(Method("sendMessage"), body, ct);
            if (response.IsSuccessStatusCode) return TelegramSendResult.Ok;

            var error = await ReadErrorAsync(response, ct);
            var detail = $"{(int)response.StatusCode}: {error.Description}";

            return response.StatusCode switch
            {
                HttpStatusCode.Forbidden => new(false, false, true, null, detail),
                HttpStatusCode.BadRequest when error.Description?.Contains("chat not found", StringComparison.OrdinalIgnoreCase) == true =>
                    new(false, false, true, null, detail),
                HttpStatusCode.BadRequest => new(false, false, false, null, detail),
                HttpStatusCode.TooManyRequests => new(false, true, false,
                    error.RetryAfter is { } s ? TimeSpan.FromSeconds(s) : null, detail),
                _ => new(false, true, false, null, detail)
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, true, false, null, "timeout");
        }
        catch (HttpRequestException ex)
        {
            return new(false, true, false, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<TelegramUpdate>?> GetUpdatesAsync(
        long offset, int timeoutSeconds, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        var body = new { offset, timeout = timeoutSeconds, allowed_updates = new[] { "message" } };

        try
        {
            using var response = await Http().PostAsJsonAsync(Method("getUpdates"), body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, ct);
                _log.LogWarning("Telegram getUpdates answered {Status}: {Detail}", (int)response.StatusCode, error.Description);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<TelegramResponse<List<TelegramUpdate>>>(ct);
            return payload?.Result ?? [];
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return [];
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning("Telegram getUpdates failed: {Error}", ex.Message);
            return null;
        }
    }

    private HttpClient Http() => _clients.CreateClient(HttpClientName);

    // Always absolute: the token contains a ':', so a relative "bot123:ABC/..." would parse as a URI
    // with scheme "bot123" and never reach Telegram.
    private Uri Method(string name) => new($"https://api.telegram.org/bot{_options.BotToken}/{name}", UriKind.Absolute);

    private static async Task<(string? Description, int? RetryAfter)> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<TelegramResponse<JsonElement>>(ct);
            return (payload?.Description, payload?.Parameters?.RetryAfter);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private sealed record TelegramResponse<T>(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("parameters")] TelegramResponseParameters? Parameters);

    private sealed record TelegramResponseParameters(
        [property: JsonPropertyName("retry_after")] int? RetryAfter);
}
