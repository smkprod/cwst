using ClanWarTracker.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClanWarTracker.Infrastructure.Telegram;

public class TelegramNotificationSender(ITelegramBotClient bot) : INotificationSender
{
    // Ссылка на Mini App строится из username бота. Определяем её один раз через GetMe
    // и кэшируем на весь процесс (username стабилен). null — пока не удалось определить.
    private static string? _cachedAppUrl;
    private static bool _resolved;

    // Кнопка «Открыть в Mini App» теперь под КАЖДЫМ сообщением бота — и в ЛС, и в чатах.
    public async Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default)
    {
        try
        {
            await SendAsync(telegramUserId, text, threadId: null, html: false, ct);
        }
        catch (ApiRequestException)
        {
            // Игрок не запускал бота / заблокировал его — Telegram отдаёт «chat not found»,
            // «bot was blocked by the user», «user is deactivated». Это ожидаемо для ЛС:
            // молча пропускаем, чтобы один недоступный получатель не срывал всю рассылку
            // (напоминания, финальный пинок, /nudge и т.д.).
        }
    }

    public Task SendToChatAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default) =>
        SendAsync(chatId, text, threadId, html, ct);

    // Оставлено для совместимости с вызовами: кнопка теперь и так добавляется всегда.
    public Task SendToChatWithAppButtonAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default) =>
        SendAsync(chatId, text, threadId, html, ct);

    private async Task SendAsync(long chatId, string text, int? threadId, bool html, CancellationToken ct)
    {
        var appUrl = await GetAppUrlAsync(ct);
        var parseMode = html ? ParseMode.Html : ParseMode.None;
        var keyboard = appUrl is null
            ? null
            : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("🎮 Открыть в Mini App", appUrl));
        await bot.SendMessage(chatId, text,
            parseMode: parseMode, replyMarkup: keyboard, messageThreadId: threadId, cancellationToken: ct);
    }

    /// <summary>https://t.me/&lt;bot&gt;?startapp открывает основное Mini App прямо из чата.</summary>
    private async Task<string?> GetAppUrlAsync(CancellationToken ct)
    {
        if (_resolved) return _cachedAppUrl;
        try
        {
            var me = await bot.GetMe(ct);
            if (me.Username is { Length: > 0 } username)
            {
                _cachedAppUrl = $"https://t.me/{username}?startapp";
                _resolved = true; // кэшируем только при успехе, иначе пробуем снова в след. раз
            }
        }
        catch { /* GetMe временно недоступен — отправим без кнопки, попробуем позже */ }
        return _cachedAppUrl;
    }
}
