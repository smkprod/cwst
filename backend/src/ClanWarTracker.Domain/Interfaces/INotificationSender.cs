namespace ClanWarTracker.Domain.Interfaces;

/// <summary>
/// Кнопка под сообщением бота - без типов Telegram, чтобы слой приложения о нём не знал.
/// Либо колбэк (бот получит нажатие), либо ссылка. Ссылка вида «startapp:plus»
/// превращается в ссылку на приложение с этим параметром запуска.
/// </summary>
/// SwitchInline - кнопка «выбрать чат и вставить туда @бот запрос»: так вызов на дуэль
/// уезжает в любой чат в два тапа.
public record BotButton(string Text, string? CallbackData = null, string? Url = null, string? SwitchInline = null);

/// <summary>
/// Итог личного сообщения. Blocked - человек не запускал бота или заблокировал его:
/// писать ему бесполезно. Временный сбой (429, 5xx, таймаут) - это MessageId null
/// без Blocked: пропускаем это сообщение, но не замолкаем навсегда.
/// </summary>
public record DmResult(int? MessageId, bool Blocked)
{
    public bool Delivered => MessageId is not null;
}

public interface INotificationSender
{
    Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default);

    /// <summary>
    /// Личное сообщение с ответом, дошло ли оно. false - человек не запускал бота
    /// или заблокировал его: вызывающий запоминает это и перестаёт пытаться.
    /// </summary>
    Task<bool> TrySendToUserAsync(long telegramUserId, string text, CancellationToken ct = default);

    /// <summary>
    /// Личное сообщение со своими кнопками (рядами). Возвращает номер сообщения, чтобы
    /// потом его править, или null - не доставлено.
    /// </summary>
    Task<int?> SendToUserWithButtonsAsync(
        long telegramUserId, string text, IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct = default);

    /// <summary>
    /// То же, но с разбором неудачи и тихой отправкой (silent - без звука и вибрации:
    /// так трекер боёв не пищит посреди захода).
    /// </summary>
    Task<DmResult> SendDmAsync(
        long telegramUserId, string text, IReadOnlyList<IReadOnlyList<BotButton>> rows, bool silent = false,
        CancellationToken ct = default);

    /// <summary>
    /// Переписывает своё сообщение в личке. rows null или пустые - кнопки убираются.
    /// false - сообщение удалено или недоступно. Тот же текст, что уже стоит, - успех.
    /// Правка не даёт уведомления - на этом держится тихая карточка трекера.
    /// </summary>
    Task<bool> EditUserMessageAsync(
        long chatId, int messageId, string text, IReadOnlyList<IReadOnlyList<BotButton>>? rows = null,
        CancellationToken ct = default);

    /// <summary>
    /// Переписывает сообщение, отправленное через инлайн-режим (@бот в чужом чате).
    /// В такой чат бот писать не может - только править свою карточку по её id.
    /// </summary>
    Task<bool> EditInlineMessageAsync(
        string inlineMessageId, string text, IReadOnlyList<IReadOnlyList<BotButton>>? rows = null,
        CancellationToken ct = default);

    /// <summary>
    /// Загружает картинки в Telegram, отправив их в этот чат (владельцу - как превью
    /// рассылки), и возвращает их file_id: дальше рассылка шлёт уже их, не гоняя
    /// файлы по сети сотни раз. Бросает исключение, если загрузить не вышло.
    /// </summary>
    Task<IReadOnlyList<string>> UploadPhotosAsync(
        long chatId, IReadOnlyList<(Stream Content, string FileName)> photos, CancellationToken ct = default);

    /// <summary>
    /// Картинки по file_id: одна - фото с подписью, несколько - альбом с подписью у
    /// первой. Бросает исключение при недоставке - вызывающий считает неудачи.
    /// </summary>
    Task SendPhotosAsync(
        long chatId, IReadOnlyList<string> fileIds, string? caption, int? threadId = null, CancellationToken ct = default);

    /// <summary>Удаляет своё сообщение в личке. false - не вышло (старше 48 часов или уже удалено).</summary>
    Task<bool> DeleteUserMessageAsync(long chatId, int messageId, CancellationToken ct = default);

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
