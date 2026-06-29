using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Ручная рассылка от владельца сервиса: произвольный текст в ЛС всем привязанным
/// игрокам и/или во все чаты кланов (в нужную тему, если клан настроен в топике).
/// Текст шлём как обычный (не HTML) — владелец пишет свободно, экранировать нечего.
/// </summary>
public class OwnerBroadcastUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    public record BroadcastResult(int SentDm, int SentChats, int FailedDm, int FailedChats);

    /// <summary>Мягкая пауза между сообщениями: Bot API душит при &gt;~30 msg/сек.</summary>
    private static readonly TimeSpan SendGap = TimeSpan.FromMilliseconds(40);

    public async Task<BroadcastResult> ExecuteAsync(string text, bool toDm, bool toChats,
        CancellationToken ct = default)
    {
        int sentDm = 0, sentChats = 0, failedDm = 0, failedChats = 0;

        if (toDm)
        {
            var ids = (await players.GetAllLinkedAsync(ct))
                .Where(p => p.TelegramUserId is not null)
                .Select(p => p.TelegramUserId!.Value)
                .Distinct(); // один человек мог привязать несколько тегов — шлём один раз
            foreach (var id in ids)
            {
                try { await notifier.SendToUserAsync(id, text, ct); sentDm++; }
                catch { failedDm++; } // заблокировал бота / удалил чат — пропускаем
                await Task.Delay(SendGap, ct);
            }
        }

        if (toChats)
        {
            foreach (var clan in await clans.GetAllAsync(ct))
            {
                if (clan.TelegramChatId == 0) continue;
                try
                {
                    await notifier.SendToChatAsync(clan.TelegramChatId, text,
                        clan.TelegramMessageThreadId, html: false, ct: ct);
                    sentChats++;
                }
                catch { failedChats++; }
                await Task.Delay(SendGap, ct);
            }
        }

        return new BroadcastResult(sentDm, sentChats, failedDm, failedChats);
    }
}
