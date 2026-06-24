namespace ClanWarTracker.Domain.Interfaces;

public interface INotificationSender
{
    Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default);
    Task SendToChatAsync(long chatId, string text, CancellationToken ct = default);

    /// <summary>То же, что SendToChatAsync, но с кнопкой «Открыть в Mini App» под сообщением
    /// (если username бота удалось определить; иначе просто текст).</summary>
    Task SendToChatWithAppButtonAsync(long chatId, string text, CancellationToken ct = default);
}
