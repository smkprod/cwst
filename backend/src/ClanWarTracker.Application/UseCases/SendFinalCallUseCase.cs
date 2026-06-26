using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Финальный отзыв» — отправляется один раз в день, ровно за минуту до конца военного
/// дня (см. SendFinalCallWorker), всем, кто ещё не доиграл 4/4. CR API не отдаёт точное
/// время сброса дня — расчёт идёт от того же допущения "сброс в 10:00 UTC", на котором уже
/// держатся напоминания и DayEndsAtUtc.
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

            var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}";
            if (!reportedKeys.Add(key)) continue;

            var slackers = war.Participants.Where(p => p.DecksUsedToday < 4).ToList();
            if (slackers.Count == 0) continue; // все уже доиграли — нечего слать

            var linked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .ToDictionary(p => p.PlayerTag, p => p.TelegramUserId, StringComparer.OrdinalIgnoreCase);

            var names = string.Join(", ", slackers.Take(20).Select(s =>
                $"{TelegramMention.Mention(s.Name, linked.GetValueOrDefault(s.PlayerTag))} ({4 - s.DecksUsedToday}/4 колод)"));
            var more = slackers.Count > 20 ? $" и ещё {slackers.Count - 20}" : "";

            await notifier.SendToChatAsync(clan.TelegramChatId,
                $"🚨 Война закрывается через минуту! Последний шанс доиграть:\n{names}{more}",
                clan.TelegramMessageThreadId, html: true, ct: ct);
            sent++;
        }

        return sent;
    }
}
