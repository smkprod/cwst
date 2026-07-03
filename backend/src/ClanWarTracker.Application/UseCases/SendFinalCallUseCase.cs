using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Последний звонок» — примерно за 30 минут до конца военного дня, всем, кто не доиграл 4/4.
/// Время конца дня: настройка клана (глава задаёт «время смены дня») → точное время из API
/// (war.DayEndsAtUtc, когда CR его отдаёт) → допущение 10:00 UTC. Вызывается часто (каждые
/// 10 минут из WarCheckWorker); отправка — только когда «сейчас» попадает в окно за ~30 минут
/// до конца, и не чаще раза в день на клан (дедуп по ключу дня).
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
        var now = DateTime.UtcNow;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;

            var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!settings.FinalCall.Enabled) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API лежит — в этот день просто не успеем напомнить

            if (war is null || !war.IsWarDay) continue;

            // Окно «за ~30 минут до конца»: настройка главы → DayEndsAtUtc (точное из API или 10:00 UTC).
            var end = settings.NextWarEndUtc(now) ?? war.DayEndsAtUtc;
            var minsToEnd = (end - now).TotalMinutes;
            if (minsToEnd is < 25 or > 35) continue; // не в окне последнего звонка

            var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}";
            if (!reportedKeys.Add(key)) continue;

            // Только текущий состав: в списке войны CR API держит и ушедших (за неделю >50).
            var members = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct);
            var roster = WarRoster.CurrentMemberTags(war, members);
            var slackers = war.Participants
                .Where(p => p.DecksUsedToday < 4 && roster.Contains(p.PlayerTag))
                .ToList();
            if (slackers.Count == 0) continue; // все уже доиграли — нечего слать

            var linked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Тегаем только привязанных — иначе получается стена имён, которая никого не пингует.
            var taggable = slackers.Where(s => linked.ContainsKey(s.PlayerTag)).ToList();
            if (taggable.Count == 0) continue;

            var names = string.Join("\n", taggable.Take(30).Select(s =>
            {
                var p = linked[s.PlayerTag];
                return $"• {TelegramMention.Mention(s.Name, p.TelegramUserId, p.TelegramUsername)} " +
                       $"— осталось {4 - s.DecksUsedToday}/4 🃏";
            }));
            var unlinked = slackers.Count - taggable.Count;
            var more = unlinked > 0 ? $"\n\n👥 Ещё <b>{unlinked}</b> не привязали Telegram." : "";

            await notifier.SendToChatAsync(clan.TelegramChatId,
                $"🚨 <b>Война закрывается через ~30 минут!</b>\nПоследний шанс доиграть КВ:\n\n{names}{more}",
                clan.TelegramMessageThreadId, html: true, ct: ct);
            sent++;
        }

        return sent;
    }
}
