using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClanWarTracker.Infrastructure.Telegram;

/// <summary>
/// Виджеты Bot API 10.4 обычными HTTP-запросами: в Telegram.Bot 22 этих методов нет.
///
/// Включаются настройкой TELEGRAM_WIDGETS=1. Адрес Bot API - TELEGRAM_WIDGET_API (по
/// умолчанию https://api.telegram.org; для беты Telegram - https://api.telegram.org/beta),
/// TELEGRAM_WIDGET_TEST_ENV=1 - тестовый сервер Telegram. Если Telegram отказал не по
/// делу конкретного чата (метода нет, бот не в списке), виджеты гаснут на час и бот
/// отвечает текстом: так включённая настройка без поддержки на сервере ничего не ломает.
/// </summary>
public sealed class TelegramWidgetSender : IWidgetSender
{
    public const string HttpClientName = "tgwidgets";
    private static readonly TimeSpan PauseAfterFailure = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory http;
    private readonly ILogger<TelegramWidgetSender> logger;
    private readonly string? baseUrl;
    private DateTime pausedUntilUtc = DateTime.MinValue;

    public TelegramWidgetSender(IHttpClientFactory http, ILogger<TelegramWidgetSender> logger,
        string? botToken, bool enabled, string? apiBase, bool testEnv)
    {
        this.http = http;
        this.logger = logger;
        if (!enabled || string.IsNullOrEmpty(botToken)) return;

        var api = string.IsNullOrWhiteSpace(apiBase) ? "https://api.telegram.org" : apiBase.Trim().TrimEnd('/');
        baseUrl = $"{api}/bot{botToken}" + (testEnv ? "/test" : "");
    }

    public bool Enabled => baseUrl is not null && DateTime.UtcNow >= pausedUntilUtc;

    public async Task<string?> SendSharedNewAsync(long chatId, string html, string publicData, string caption,
        int? threadId = null, CancellationToken ct = default)
    {
        var (ok, result, _) = await CallAsync("sendWidget", new
        {
            chat_id = chatId,
            message_thread_id = threadId,
            html,
            public_data = publicData,
            caption,
            is_shared = true,
        }, ct);
        if (!ok) return null;

        // Отправленное сообщение несёт виджет: { widget: { id, ... } }
        var id = result?["widget"]?["id"]?.ToString();
        if (string.IsNullOrEmpty(id)) logger.LogWarning("sendWidget: no widget id in the answer");
        return string.IsNullOrEmpty(id) ? null : id;
    }

    public async Task<bool> SendSharedAsync(long chatId, string widgetId, string caption,
        int? threadId = null, CancellationToken ct = default)
    {
        var (ok, _, _) = await CallAsync("sendSharedWidget", new
        {
            chat_id = chatId,
            message_thread_id = threadId,
            widget_id = widgetId,
            caption,
        }, ct);
        return ok;
    }

    public async Task<WidgetEditResult> EditSharedAsync(string widgetId, string publicData, int cacheTimeSeconds,
        CancellationToken ct = default)
    {
        var (ok, _, error) = await CallAsync("editSharedWidgetData", new
        {
            widget_id = widgetId,
            public_data = publicData,
            cache_time = Math.Clamp(cacheTimeSeconds, 5, 3600),
        }, ct, pauseOnFailure: false);
        if (ok) return WidgetEditResult.Ok;
        if (error?.Contains("not modified", StringComparison.OrdinalIgnoreCase) == true) return WidgetEditResult.NotModified;
        if (error?.Contains("WIDGET_ID_INVALID", StringComparison.OrdinalIgnoreCase) == true) return WidgetEditResult.Gone;
        return WidgetEditResult.Failed;
    }

    private async Task<(bool Ok, JsonNode? Result, string? Error)> CallAsync(
        string method, object body, CancellationToken ct, bool pauseOnFailure = true)
    {
        if (!Enabled) return (false, null, "disabled");
        try
        {
            using var client = http.CreateClient(HttpClientName);
            using var resp = await client.PostAsJsonAsync($"{baseUrl}/{method}", body, JsonOpts, ct);
            var json = await resp.Content.ReadFromJsonAsync<JsonNode>(ct);
            if (json?["ok"]?.GetValue<bool>() == true) return (true, json["result"], null);

            var code = json?["error_code"]?.GetValue<int>() ?? (int)resp.StatusCode;
            var description = json?["description"]?.ToString() ?? resp.ReasonPhrase;
            logger.LogWarning("Telegram {Method} failed: {Code} {Description}", method, code, description);

            // 404 - метода нет (Bot API без виджетов), 401/403 на сам метод - бот не допущен:
            // дело не в чате, и пробовать каждое сообщение заново бессмысленно.
            // 400 - чат или данные: на следующем чате может выйти.
            if (pauseOnFailure && code is 401 or 404 or 501) Pause();
            return (false, null, description);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Telegram {Method} failed", method);
            return (false, null, ex.Message);
        }
    }

    private void Pause()
    {
        pausedUntilUtc = DateTime.UtcNow + PauseAfterFailure;
        logger.LogWarning("Telegram widgets paused until {Until:u}: falling back to text", pausedUntilUtc);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
