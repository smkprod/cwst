using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Анонс начала Клановой войны: когда у клана наступает ПЕРВЫЙ военный день недели
/// (в обычном расписании River Race это четверг), бот пишет в чат клана и в ЛС
/// привязанным игрокам, что КВ началась и пора отыграть колоды.
/// CR API не даёт «день недели» как четверг/пятница — ориентируемся на состояние игры
/// (periodIndex == 3 = первый военный день), это надёжнее хардкода дня недели и само
/// учитывает колизей и сдвиги сезона.
/// </summary>
public class SendWarStartUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    private static readonly TimeSpan SendGap = TimeSpan.FromMilliseconds(40);

    /// <param name="announcedKeys">Дедуп между тиками: "clanId:season:section" — один анонс на военную неделю.</param>
    /// <returns>Сколько кланов получили анонс в чат.</returns>
    public async Task<int> ExecuteAsync(ISet<string> announcedKeys, CancellationToken ct = default)
    {
        var sent = 0;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; }

            if (war is null || !war.IsWarDay) continue;
            if (war.PeriodIndex != 3) continue; // только первый военный день недели = старт КВ

            var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!settings.WarStart.Enabled) continue;

            var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}";
            if (!announcedKeys.Add(key)) continue;

            var isColosseum = war.PeriodType == "colosseum";
            var title = isColosseum ? "🏟 Колизей начался!" : "⚔️ Клановая война началась!";

            if (clan.TelegramChatId != 0 && settings.WarStart.Channel.WantsChat())
            {
                try
                {
                    await notifier.SendToChatWithAppButtonAsync(clan.TelegramChatId,
                        $"{title}\nПора отыграть 4/4 колоды — не подведи клан! 💪",
                        clan.TelegramMessageThreadId, html: false, ct: ct);
                    sent++;
                }
                catch { /* чат удалён / бот выкинут — не критично */ }
            }

            if (!settings.WarStart.Channel.WantsDm()) continue;

            var linked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null);
            foreach (var p in linked)
            {
                try
                {
                    await notifier.SendToUserAsync(p.TelegramUserId!.Value,
                        $"{title}\nЗайди и отыграй 4/4 колоды за клан {clan.Name}. Удачи! 🍀", ct);
                }
                catch { /* заблокировал бота — пропускаем */ }
                await Task.Delay(SendGap, ct);
            }
        }

        return sent;
    }
}
