using System.Net;
using System.Text.RegularExpressions;
using ClanWarTracker.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClanWarTracker.Infrastructure.Telegram;

public partial class TelegramChannelPublisher(ITelegramBotClient bot) : IChannelPublisher
{
    /// <summary>Подпись к фото у Telegram короче текста сообщения: 1024 символа против 4096.</summary>
    private const int CaptionLimit = 1024;

    private static string? _botUsername;
    private static long _botId;

    public async Task<ChannelInfo?> ResolveAsync(string handle, CancellationToken ct = default)
    {
        var id = Normalize(handle);
        if (id is null) return null;
        try
        {
            var chat = await bot.GetChat(id, ct);
            if (chat.Type != ChatType.Channel) return null;
            await EnsureMeAsync(ct);
            var member = await bot.GetChatMember(chat.Id, _botId, ct);
            var canPost = member is ChatMemberAdministrator admin && admin.CanPostMessages == true;
            return new ChannelInfo(chat.Id, chat.Title ?? chat.Username ?? chat.Id.ToString(), chat.Username, canPost);
        }
        catch (ApiRequestException) { return null; }
        catch (HttpRequestException) { return null; }
    }

    /// <summary>«@name», «t.me/name», «https://t.me/name» или «-100…» → то, что понимает Telegram.</summary>
    private static ChatId? Normalize(string handle)
    {
        var h = handle.Trim();
        if (h.Length == 0) return null;
        if (long.TryParse(h, out var numeric)) return numeric;
        var m = HandleRegex().Match(h);
        if (!m.Success) return null;
        return "@" + m.Groups[1].Value;
    }

    [GeneratedRegex(@"^(?:(?:https?://)?(?:t\.me|telegram\.me)/|@)?([A-Za-z][A-Za-z0-9_]{3,31})/?$")]
    private static partial Regex HandleRegex();

    public async Task<int?> PublishAsync(long channelId, string html, string? photoUrl,
        IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct = default)
    {
        try
        {
            var keyboard = await KeyboardAsync(rows, ct);
            if (!string.IsNullOrWhiteSpace(photoUrl) && VisibleLength(html) <= CaptionLimit)
            {
                var m = await bot.SendPhoto(channelId, InputFile.FromUri(photoUrl), caption: html,
                    parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct);
                return m.MessageId;
            }

            // Текст длиннее подписи - картинка становится большим превью над ним: одно
            // сообщение вместо фото и отдельного текста, которые в ленте разъезжаются.
            var preview = string.IsNullOrWhiteSpace(photoUrl)
                ? new LinkPreviewOptions { IsDisabled = true }
                : new LinkPreviewOptions { Url = photoUrl, PreferLargeMedia = true, ShowAboveText = true };
            var sent = await bot.SendMessage(channelId, html, parseMode: ParseMode.Html,
                linkPreviewOptions: preview, replyMarkup: keyboard, cancellationToken: ct);
            return sent.MessageId;
        }
        catch (ApiRequestException) { return null; }
        catch (HttpRequestException) { return null; }
    }

    /// <summary>Длина текста, как её считает Telegram: без тегов и с раскрытыми сущностями.</summary>
    private static int VisibleLength(string html) => WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Length;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    private async Task EnsureMeAsync(CancellationToken ct)
    {
        if (_botId != 0) return;
        var me = await bot.GetMe(ct);
        _botId = me.Id;
        _botUsername = me.Username;
    }

    private async Task<InlineKeyboardMarkup?> KeyboardAsync(IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return null;
        try { await EnsureMeAsync(ct); } catch { /* без юзернейма - кнопки в приложение пропустим */ }
        var built = new List<InlineKeyboardButton[]>();
        foreach (var row in rows)
        {
            var buttons = new List<InlineKeyboardButton>();
            foreach (var b in row)
            {
                if (b.Url is not { Length: > 0 } url) continue;
                if (url.StartsWith("startapp:", StringComparison.Ordinal))
                {
                    if (_botUsername is null) continue;
                    var param = url["startapp:".Length..];
                    buttons.Add(InlineKeyboardButton.WithUrl(b.Text,
                        param.Length == 0 ? $"https://t.me/{_botUsername}?startapp" : $"https://t.me/{_botUsername}?startapp={param}"));
                }
                else if (url == "bot:")
                {
                    if (_botUsername is not null) buttons.Add(InlineKeyboardButton.WithUrl(b.Text, $"https://t.me/{_botUsername}"));
                }
                else buttons.Add(InlineKeyboardButton.WithUrl(b.Text, url));
            }
            if (buttons.Count > 0) built.Add(buttons.ToArray());
        }
        return built.Count == 0 ? null : new InlineKeyboardMarkup(built);
    }
}
