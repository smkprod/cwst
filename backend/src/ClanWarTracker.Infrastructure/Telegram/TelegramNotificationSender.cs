using ClanWarTracker.Domain.Interfaces;
using Telegram.Bot;

namespace ClanWarTracker.Infrastructure.Telegram;

public class TelegramNotificationSender(ITelegramBotClient bot) : INotificationSender
{
    public Task SendToUserAsync(long telegramUserId, string text, CancellationToken ct = default) =>
        bot.SendMessage(telegramUserId, text, cancellationToken: ct);

    public Task SendToChatAsync(long chatId, string text, CancellationToken ct = default) =>
        bot.SendMessage(chatId, text, cancellationToken: ct);
}
