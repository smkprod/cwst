using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Авто-отчёт в чат клана после каждого военного дня (Pro).
/// Воркер вызывает каждые 30 минут; день считается закрытым, когда финальный
/// снимок дня перестал быть текущим периодом. Отчёт шлём один раз — окно 2 часа
/// после последнего обновления снимка + дедуп по ключу (переживает почти всё,
/// кроме рестарта воркера прямо внутри окна).
/// </summary>
public class SendDailyReportUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IWarSnapshotRepository snapshots,
    INotificationSender notifier)
{
    /// <summary>Не репостим день, чей финальный снимок старше этого окна.</summary>
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(2);

    /// <param name="reportedKeys">Дедуп между тиками: "clanId:season:section:period".</param>
    /// <returns>Сколько отчётов отправлено.</returns>
    public async Task<int> ExecuteAsync(ISet<string> reportedKeys, CancellationToken ct = default)
    {
        var sent = 0;
        var now = DateTime.UtcNow;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;
            if (clan.EffectivePlan(now) != PlanTier.Pro) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API лежит — попробуем в следующем тике

            if (war is null) continue;

            // Последняя неделя с данными; финальный снимок последнего военного дня
            var lastWeek = await snapshots.GetByClanAsync(clan.Id, weeks: 1, ct);
            var final = lastWeek
                .OrderByDescending(s => s.SeasonId)
                .ThenByDescending(s => s.SectionIndex)
                .ThenByDescending(s => s.PeriodIndex)
                .FirstOrDefault();
            if (final is null) continue;

            // День ещё идёт? (снимок совпадает с текущим периодом войны)
            var isCurrentPeriod = war.IsWarDay &&
                final.SeasonId == war.SeasonId &&
                final.SectionIndex == war.SectionIndex &&
                final.PeriodIndex == war.PeriodIndex;
            if (isCurrentPeriod) continue;

            // Старые новости не постим (день закончился давно)
            if (now - final.CapturedAtUtc > FreshnessWindow) continue;

            var key = $"{clan.Id}:{final.SeasonId}:{final.SectionIndex}:{final.PeriodIndex}";
            if (!reportedKeys.Add(key)) continue;

            var text = await BuildReportAsync(clan, final, ct);
            await notifier.SendToChatAsync(clan.TelegramChatId, text, ct);
            sent++;
        }
        return sent;
    }

    private async Task<string> BuildReportAsync(Clan clan, WarSnapshot final, CancellationToken ct)
    {
        var dayNumber = final.PeriodIndex - 2;          // 3..6 -> 1..4
        var isWeekFinal = final.PeriodIndex >= 6;
        var isColosseum = final.PeriodType == "colosseum";

        // Слава за день = накопительная минус снимок предыдущего дня
        var prevDay = final.PeriodIndex > 3
            ? await snapshots.GetSnapshotAsync(clan.Id, final.SeasonId, final.SectionIndex,
                final.PeriodIndex - 1, ct)
            : null;
        var prevFameByTag = (prevDay?.Players ?? [])
            .ToDictionary(p => p.PlayerTag, p => p.Fame, StringComparer.OrdinalIgnoreCase);

        var dayResults = final.Players
            .Select(p => (p.Name, p.DecksUsedToday,
                DayFame: p.Fame - prevFameByTag.GetValueOrDefault(p.PlayerTag, 0)))
            .ToList();

        var dayFame = dayResults.Sum(r => r.DayFame);
        var top = dayResults.Where(r => r.DayFame > 0).OrderByDescending(r => r.DayFame).Take(3).ToList();
        var slackers = dayResults.Where(r => r.DecksUsedToday < 4).ToList();

        var medals = new[] { "🥇", "🥈", "🥉" };
        var lines = new List<string>
        {
            isWeekFinal
                ? $"🏁 {(isColosseum ? "Колизей" : "Война")} завершён{(isColosseum ? "" : "а")}! Итоги последнего дня:"
                : $"🌙 День {dayNumber} войны завершён!",
            $"🏅 Слава за день: {dayFame:N0}",
        };

        if (top.Count > 0)
        {
            lines.Add("");
            lines.Add("Лучшие за день:");
            lines.AddRange(top.Select((r, i) => $"{medals[i]} {r.Name} — {r.DayFame:N0}"));
        }

        if (slackers.Count > 0)
        {
            lines.Add("");
            var names = string.Join(", ", slackers.Take(15).Select(s => $"{s.Name} ({s.DecksUsedToday}/4)"));
            var more = slackers.Count > 15 ? $" и ещё {slackers.Count - 15}" : "";
            lines.Add($"😴 Не доиграли: {names}{more}");
        }
        else
        {
            lines.Add("");
            lines.Add("💪 Все отыграли 4/4 — идеальный день!");
        }

        return string.Join("\n", lines);
    }
}
