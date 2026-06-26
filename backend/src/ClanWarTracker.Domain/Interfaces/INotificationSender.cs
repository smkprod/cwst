namespace ClanWarTracker.Domain.Interfaces;

public interface INotificationSender
{
    Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default);

    /// <param name="threadId">ID темы (Topic) форума группы — если клан настроен через /setup
    /// внутри темы, сообщение нужно слать туда, а не в общий чат. Null — общий чат/группа без тем.</param>
    /// <param name="html">Текст уже содержит HTML-разметку (например, &lt;a href="tg://user?id=..."&gt;
    /// упоминания игроков) — отправлять с ParseMode.Html вместо обычного текста.</param>
    Task SendToChatAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default);

    /// <summary>То же, что SendToChatAsync, но с кнопкой «Открыть в Mini App» под сообщением
    /// (если username бота удалось определить; иначе просто текст).</summary>
    Task SendToChatWithAppButtonAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default);
}
