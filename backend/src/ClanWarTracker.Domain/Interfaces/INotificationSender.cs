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

    /// <summary>
    /// Картинка в чат с подписью. photoUrl — публичный адрес, картинку качает сам
    /// Telegram; передавать байты не нужно и не стоит.
    ///
    /// Возвращает false, если отправить не удалось: вызывающий тогда шлёт текстом.
    /// Поздравление без картинки лучше, чем отсутствие поздравления.
    /// </summary>
    Task<bool> SendPhotoToChatAsync(
        long chatId, string photoUrl, string caption, int? threadId = null, CancellationToken ct = default);

    /// <summary>То же, что SendToChatAsync, но с кнопкой «Открыть в Mini App» под сообщением
    /// (если username бота удалось определить; иначе просто текст).</summary>
    Task SendToChatWithAppButtonAsync(
        long chatId, string text, int? threadId = null, bool html = false, CancellationToken ct = default);

    /// <summary>
    /// Публикует сообщение и возвращает его id, чтобы потом редактировать. Нужно для
    /// живого табло: одно сообщение, которое обновляется, вместо потока новых.
    /// null — отправить не вышло.
    /// </summary>
    Task<int?> PostAsync(long chatId, string text, int? threadId = null, CancellationToken ct = default);

    /// <summary>
    /// Переписывает ранее отправленное сообщение. false — сообщение удалили, бота
    /// выгнали или текст не изменился; вызывающий решает, публиковать ли заново.
    /// </summary>
    Task<bool> EditAsync(long chatId, int messageId, string text, CancellationToken ct = default);

    /// <summary>Закрепляет сообщение в чате. Молча ничего не делает без прав администратора.</summary>
    Task PinAsync(long chatId, int messageId, CancellationToken ct = default);
}
