using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Последний звонок» — примерно за 30 минут до конца военного дня, всем, кто не доиграл 4/4.
/// Время конца дня берётся из настроек клана (глава задаёт «во сколько заканчивается КВ»),
/// иначе — допущение по умолчанию (10:00 UTC). Вызывается часто (каждые 10 минут из
/// WarCheckWorker); отправка происходит только когда «сейчас» попадает в окно за ~30 минут
/// до конца, и не чаще раза в день на клан (дедуп по ключу дня).
/// </summary>
public class SendFinalCallUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    // Допущение по умолчанию, если глава не задал время: сброс дня в 10:00 UTC.
    private const int DefaultEndMinuteUtc = 10 * 60;

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

            // Окно «за ~30 минут до конца»: конец берём из настройки клана, иначе 10:00 UTC.
            var end = settings.NextWarEndUtc(now)
                      ?? NextMinuteOfDayUtc(now, DefaultEndMinuteUtc);
            var minsToEnd = (end - now).TotalMinutes;
            if (minsToEnd is < 25 or > 35) continue; // не в окне последнего звонка

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API лежит — в этот день просто не успеем напомнить

            if (war is null || !war.IsWarDay) continue;

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

    private static DateTime NextMinuteOfDayUtc(DateTime nowUtc, int minuteOfDay)
    {
        var t = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, minuteOfDay / 60, minuteOfDay % 60, 0, DateTimeKind.Utc);
        return nowUtc < t ? t : t.AddDays(1);
    }
}
