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

    public async Task<bool> TrySendToUserAsync(long telegramUserId, string text, CancellationToken ct = default)
    {
        try
        {
            await SendAsync(telegramUserId, text, threadId: null, html: false, ct);
            return true;
        }
        catch (ApiRequestException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    public async Task<int?> SendToUserWithButtonsAsync(
        long telegramUserId, string text, IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct = default) =>
        (await SendDmAsync(telegramUserId, text, rows, false, ct)).MessageId;

    public async Task<DmResult> SendDmAsync(
        long telegramUserId, string text, IReadOnlyList<IReadOnlyList<BotButton>> rows, bool silent = false,
        CancellationToken ct = default)
    {
        try
        {
            var keyboard = await KeyboardAsync(rows, ct);
            var message = await bot.SendMessage(telegramUserId, text, replyMarkup: keyboard,
                disableNotification: silent, cancellationToken: ct);
            return new DmResult(message.MessageId, false);
        }
        catch (ApiRequestException ex)
        {
            // Заблокирован - только 403 и «чата нет / аккаунт удалён». Лимит (429) и
            // сбои Telegram - временные: раньше они глушили платящего до его же сообщения боту.
            var blocked = ex.ErrorCode == 403
                || (ex.ErrorCode == 400 && (ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase)
                                            || ex.Message.Contains("user is deactivated", StringComparison.OrdinalIgnoreCase)));
            return new DmResult(null, blocked);
        }
        catch (HttpRequestException) { return new DmResult(null, false); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new DmResult(null, false); }
    }

    public async Task<bool> EditUserMessageAsync(
        long chatId, int messageId, string text, IReadOnlyList<IReadOnlyList<BotButton>>? rows = null,
        CancellationToken ct = default)
    {
        try
        {
            // Пустая клавиатура, а не null: null оставил бы прежние кнопки под новым текстом
            var keyboard = rows is { Count: > 0 }
                ? await KeyboardAsync(rows, ct)
                : new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>());
            await bot.EditMessageText(chatId, messageId, text, replyMarkup: keyboard, cancellationToken: ct);
            return true;
        }
        // Тот же текст - сообщение на месте и уже правильное: для вызывающего это успех,
        // иначе трекер решил бы, что карточки нет, и прислал новую.
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        catch (ApiRequestException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    public async Task<bool> EditInlineMessageAsync(
        string inlineMessageId, string text, IReadOnlyList<IReadOnlyList<BotButton>>? rows = null,
        CancellationToken ct = default)
    {
        try
        {
            var keyboard = rows is { Count: > 0 }
                ? await KeyboardAsync(rows, ct)
                : new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>());
            await bot.EditMessageText(inlineMessageId, text, replyMarkup: keyboard,
                linkPreviewOptions: new global::Telegram.Bot.Types.LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        catch (ApiRequestException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    public async Task<IReadOnlyList<string>> UploadPhotosAsync(
        long chatId, IReadOnlyList<(Stream Content, string FileName)> photos, CancellationToken ct = default)
    {
        if (photos.Count == 1)
        {
            var m = await bot.SendPhoto(chatId,
                global::Telegram.Bot.Types.InputFile.FromStream(photos[0].Content, photos[0].FileName), cancellationToken: ct);
            return [m.Photo!.Last().FileId];
        }
        var album = photos.Select(p => (global::Telegram.Bot.Types.IAlbumInputMedia)new global::Telegram.Bot.Types.InputMediaPhoto(
            global::Telegram.Bot.Types.InputFile.FromStream(p.Content, p.FileName)));
        var sent = await bot.SendMediaGroup(chatId, album, cancellationToken: ct);
        // Самый крупный размер - последний в списке
        return sent.Select(m => m.Photo!.Last().FileId).ToList();
    }

    public async Task SendPhotosAsync(
        long chatId, IReadOnlyList<string> fileIds, string? caption, int? threadId = null, CancellationToken ct = default)
    {
        if (fileIds.Count == 1)
        {
            await bot.SendPhoto(chatId, global::Telegram.Bot.Types.InputFile.FromFileId(fileIds[0]),
                caption: caption, messageThreadId: threadId, cancellationToken: ct);
            return;
        }
        var album = fileIds.Select((id, i) => (global::Telegram.Bot.Types.IAlbumInputMedia)new global::Telegram.Bot.Types.InputMediaPhoto(
            global::Telegram.Bot.Types.InputFile.FromFileId(id)) { Caption = i == 0 ? caption : null });
        await bot.SendMediaGroup(chatId, album, messageThreadId: threadId, cancellationToken: ct);
    }

    public async Task<bool> DeleteUserMessageAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        try
        {
            await bot.DeleteMessage(chatId, messageId, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    private async Task<InlineKeyboardMarkup?> KeyboardAsync(IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return null;
        var appUrl = await GetAppUrlAsync(ct);
        var built = new List<InlineKeyboardButton[]>();
        foreach (var row in rows)
        {
            var buttons = new List<InlineKeyboardButton>();
            foreach (var b in row)
            {
                if (b.CallbackData is { } data)
                    buttons.Add(InlineKeyboardButton.WithCallbackData(b.Text, data));
                else if (b.SwitchInline is { } query)
                    buttons.Add(InlineKeyboardButton.WithSwitchInlineQuery(b.Text, query));
                else if (b.Url is { } url && url.StartsWith("startapp:", StringComparison.Ordinal))
                {
                    // Ссылка на приложение с параметром запуска; без юзернейма бота кнопку пропускаем
                    if (appUrl is not null) buttons.Add(InlineKeyboardButton.WithUrl(b.Text, appUrl + "=" + url["startapp:".Length..]));
                }
                else if (b.Url is { } plain)
                    buttons.Add(InlineKeyboardButton.WithUrl(b.Text, plain));
            }
            if (buttons.Count > 0) built.Add(buttons.ToArray());
        }
        return built.Count == 0 ? null : new InlineKeyboardMarkup(built);
    }

    public Task SendToChatAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default) =>
        SendAsync(chatId, text, threadId, html, ct);

    // Оставлено для совместимости с вызовами: кнопка теперь и так добавляется всегда.
    public Task SendToChatWithAppButtonAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default) =>
        SendAsync(chatId, text, threadId, html, ct);

    public async Task<bool> SendToChatWithButtonsAsync(
        long chatId, string text, IReadOnlyList<IReadOnlyList<BotButton>> rows, int? threadId = null,
        CancellationToken ct = default)
    {
        try
        {
            var keyboard = await KeyboardAsync(rows, ct);
            await bot.SendMessage(chatId, text,
                replyMarkup: keyboard, messageThreadId: threadId, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException) { return false; }
        catch (HttpRequestException) { return false; }
    }

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
