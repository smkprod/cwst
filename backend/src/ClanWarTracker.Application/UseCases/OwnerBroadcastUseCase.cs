using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Ручная рассылка от владельца сервиса: текст и/или картинки в ЛС всем привязанным
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

    /// <summary>Подпись к фото в Telegram - до 1024 символов; длиннее - текст отдельным сообщением.</summary>
    public const int CaptionLimit = 1024;

    public async Task<BroadcastResult> ExecuteAsync(string text, bool toDm, bool toChats,
        CancellationToken ct = default, IReadOnlyList<string>? photoIds = null)
    {
        int sentDm = 0, sentChats = 0, failedDm = 0, failedChats = 0;
        var photos = photoIds is { Count: > 0 } ? photoIds : null;
        var hasText = !string.IsNullOrWhiteSpace(text);
        var caption = photos is not null && hasText && text.Length <= CaptionLimit ? text : null;
        // Текст отдельно: без картинок, или он не влез в подпись
        var separateText = hasText && caption is null;

        if (toDm)
        {
            var ids = (await players.GetAllLinkedAsync(ct))
                .Where(p => p.TelegramUserId is not null)
                .Select(p => p.TelegramUserId!.Value)
                .Distinct(); // один человек мог привязать несколько тегов — шлём один раз
            foreach (var id in ids)
            {
                var ok = true;
                if (photos is not null)
                {
                    try { await notifier.SendPhotosAsync(id, photos, caption, null, ct); }
                    catch { ok = false; } // заблокировал бота / удалил чат — пропускаем
                }
                if (ok && separateText) ok = await notifier.TrySendToUserAsync(id, text, ct);
                if (ok) sentDm++; else failedDm++;
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
                    if (photos is not null)
                        await notifier.SendPhotosAsync(clan.TelegramChatId, photos, caption, clan.TelegramMessageThreadId, ct);
                    if (separateText)
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
