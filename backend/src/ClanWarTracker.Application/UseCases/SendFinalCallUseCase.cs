using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Последний звонок» — отправляется один раз в день, примерно за 30 минут до конца
/// военного дня (см. WarCheckWorker.RunFinalCallLoopAsync), всем, кто ещё не доиграл 4/4.
/// За полчаса реально успеть доиграть колоды — в отличие от «за минуту». CR API не отдаёт
/// точное время сброса дня — расчёт идёт от допущения "сброс в 10:00 UTC".
/// </summary>
public class SendFinalCallUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    /// <param name="reportedKeys">Дедуп между тиками: "clanId:season:section:period".</param>
    /// <returns>Сколько финальных предупреждений отправлено.</returns>
    public async Task<int> ExecuteAsync(ISet<string> reportedKeys, CancellationToken ct = default)
    {
        var sent = 0;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API лежит — в этот день просто не успеем напомнить

            if (war is null || !war.IsWarDay) continue;

            if (!NotificationSettings.Parse(clan.NotificationSettingsJson).FinalCall.Enabled) continue;

            var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}";
            if (!reportedKeys.Add(key)) continue;

            var slackers = war.Participants.Where(p => p.DecksUsedToday < 4).ToList();
            if (slackers.Count == 0) continue; // все уже доиграли — нечего слать

            var linked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .ToDictionary(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase);

            var names = string.Join(", ", slackers.Take(20).Select(s =>
            {
                var p = linked.GetValueOrDefault(s.PlayerTag);
                return $"{TelegramMention.Mention(s.Name, p?.TelegramUserId, p?.TelegramUsername)} ({4 - s.DecksUsedToday}/4 колод)";
            }));
            var more = slackers.Count > 20 ? $" и ещё {slackers.Count - 20}" : "";

            await notifier.SendToChatAsync(clan.TelegramChatId,
                $"🚨 Война закрывается через ~30 минут! Последний шанс доиграть КВ:\n{names}{more}",
                clan.TelegramMessageThreadId, html: true, ct: ct);
            sent++;
        }

        return sent;
    }
}
