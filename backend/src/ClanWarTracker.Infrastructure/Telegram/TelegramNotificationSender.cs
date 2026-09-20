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

    public async Task<bool> SendPhotoToChatAsync(
        long chatId, string photoUrl, string caption, int? threadId = null, CancellationToken ct = default)
    {
        try
        {
            var appUrl = await GetAppUrlAsync(ct);
            var keyboard = appUrl is null
                ? null
                : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("🎮 Открыть в Mini App", appUrl));

            await bot.SendPhoto(chatId, global::Telegram.Bot.Types.InputFile.FromUri(photoUrl),
                caption: caption, replyMarkup: keyboard, messageThreadId: threadId, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException)
        {
            // Telegram не смог скачать картинку, адрес недоступен снаружи, чат закрыт —
            // причин много, и ни одна не повод остаться вообще без поздравления.
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<int?> PostAsync(long chatId, string text, int? threadId = null, CancellationToken ct = default)
    {
        try
        {
            var appUrl = await GetAppUrlAsync(ct);
            var keyboard = appUrl is null
                ? null
                : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("🎮 Открыть в Mini App", appUrl));

            var message = await bot.SendMessage(chatId, text,
                replyMarkup: keyboard, messageThreadId: threadId, cancellationToken: ct);
            return message.MessageId;
        }
        catch (ApiRequestException) { return null; }
        catch (HttpRequestException) { return null; }
    }

    public async Task<bool> EditAsync(long chatId, int messageId, string text, CancellationToken ct = default)
    {
        try
        {
            var appUrl = await GetAppUrlAsync(ct);
            var keyboard = appUrl is null
                ? null
                : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("🎮 Открыть в Mini App", appUrl));

            await bot.EditMessageText(chatId, messageId, text, replyMarkup: keyboard, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException)
        {
            // Сообщение удалили, бота выгнали или текст совпал с прежним («message is not
            // modified»). Последнее — не ошибка, но и отличать его по строке не стоит:
            // вызывающий в любом случае просто попробует опубликовать табло заново.
            return false;
        }
        catch (HttpRequestException) { return false; }
    }

    public async Task PinAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        try
        {
            // Без звука: табло обновляется часто, и уведомлять о каждом закреплении незачем.
            await bot.PinChatMessage(chatId, messageId, disableNotification: true, cancellationToken: ct);
        }
        catch (ApiRequestException) { /* бот не админ — табло просто не будет закреплено */ }
        catch (HttpRequestException) { }
    }

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
