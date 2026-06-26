using System.Text;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Упоминание игрока в чате: если он привязал Telegram (TelegramUserId известен),
/// кликабельная ссылка tg://user?id=... уведомляет его напрямую (как обычный @mention),
/// даже если у него нет username. Иначе — просто имя из CR.
/// Сообщения с упоминаниями шлём с ParseMode.Html — поэтому имя экранируем.
/// </summary>
public static class TelegramMention
{
    public static string Mention(string name, long? telegramUserId) =>
        telegramUserId is { } id
            ? $"<a href=\"tg://user?id={id}\">{Escape(name)}</a>"
            : Escape(name);

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
