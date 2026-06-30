using System.Text;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Упоминание игрока в чате. Приоритет:
/// 1) @username — видимый тег, который пингует (если username публичный известен);
/// 2) кликабельная ссылка tg://user?id=... — пингует даже без username (нужен TelegramUserId);
/// 3) просто имя из CR — если ничего не привязано.
/// Сообщения с упоминаниями шлём с ParseMode.Html — поэтому имя экранируем.
/// (Символы username — [A-Za-z0-9_], экранировать не нужно.)
/// </summary>
public static class TelegramMention
{
    public static string Mention(string name, long? telegramUserId, string? username = null)
    {
        if (!string.IsNullOrEmpty(username)) return "@" + username;
        return telegramUserId is { } id
            ? $"<a href=\"tg://user?id={id}\">{Escape(name)}</a>"
            : Escape(name);
    }

    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
