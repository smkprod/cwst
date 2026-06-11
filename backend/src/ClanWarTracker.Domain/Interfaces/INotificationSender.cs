namespace ClanWarTracker.Domain.Interfaces;

public interface INotificationSender
{
    Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default);
    Task SendToChatAsync(long chatId, string text, CancellationToken ct = default);
}
