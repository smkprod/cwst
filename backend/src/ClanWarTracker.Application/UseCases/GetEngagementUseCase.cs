using ClanWarTracker.Application.Analytics;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Откуда приходят и возвращаются ли - для панели владельца. Отвечает на вопрос «во что
/// вкладываться»: какие кнопки бота приводят людей в приложение и какая доля новых
/// заходит снова.
/// </summary>
public class GetEngagementUseCase(IAppOpenRepository opens, IActivityRepository activity)
{
    /// <param name="NewUsers30">Сколько ещё не привязанных пришло по этому источнику за 30 дней - новые люди.</param>
    public record SourceRow(string Source, int Users7, int Users30, int NewUsers30);

    public record DestRow(string Dest, int Users30);

    /// <param name="Cohort">Сколько человек впервые зашло 8-37 дней назад (у них уже видно, вернулись ли за неделю).</param>
    public record RetentionDto(int Cohort, int NextDay, int Week, double NextDayPercent, double WeekPercent);

    /// <param name="AvgDau">Среднее число заходивших в день за последние 7 дней.</param>
    /// <param name="TrackingSince">С какого дня пишутся источники; null - заходов с метками ещё не было.</param>
    public record Dto(
        List<SourceRow> Sources, List<DestRow> Dests, RetentionDto Retention,
        double AvgDau, int Wau, int Mau, double Stickiness, string? TrackingSince);

    public async Task<Dto> ExecuteAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        string Day(int back) => today.AddDays(-back).ToString("yyyy-MM-dd");

        // --- Источники ---
        var rows = await opens.GetSinceAsync(Day(29), ct);
        var week = Day(6);
        var sources = OpenSource.All
            .Select(s =>
            {
                var bySource = rows.Where(r => r.Source == s).ToList();
                return new SourceRow(s,
                    bySource.Where(r => string.CompareOrdinal(r.DayUtc, week) >= 0).Select(r => r.TelegramUserId).Distinct().Count(),
                    bySource.Select(r => r.TelegramUserId).Distinct().Count(),
                    bySource.Where(r => !r.Linked).Select(r => r.TelegramUserId).Distinct().Count());
            })
            .Where(r => r.Users30 > 0)
            .OrderByDescending(r => r.Users30)
            .ToList();
        var dests = rows
            .GroupBy(r => r.Dest)
            .Select(g => new DestRow(g.Key, g.Select(r => r.TelegramUserId).Distinct().Count()))
            .OrderByDescending(d => d.Users30)
            .Take(12)
            .ToList();

        // --- Удержание и регулярность (по дням активности привязанных игроков) ---
        var days = await activity.GetAllDaysAsync(ct);
        var byPlayer = days
            .GroupBy(d => d.PlayerId)
            .ToDictionary(g => g.Key, g => g.Select(d => DateOnly.Parse(d.DayUtc)).ToHashSet());

        int cohort = 0, nextDay = 0, weekBack = 0;
        foreach (var set in byPlayer.Values)
        {
            var first = set.Min();
            var age = today.DayNumber - first.DayNumber;
            if (age < 8 || age > 37) continue;
            cohort++;
            if (set.Contains(first.AddDays(1))) nextDay++;
            if (Enumerable.Range(1, 7).Any(i => set.Contains(first.AddDays(i)))) weekBack++;
        }

        int ActiveIn(int daysBack) => byPlayer.Values.Count(set => set.Any(d => today.DayNumber - d.DayNumber < daysBack));
        var avgDau = Enumerable.Range(0, 7)
            .Average(i => byPlayer.Values.Count(set => set.Contains(today.AddDays(-i))));
        var wau = ActiveIn(7);
        var mau = ActiveIn(30);

        string? since = null;
        if (rows.Count > 0) since = rows.Min(r => r.DayUtc);

        return new Dto(sources, dests,
            new RetentionDto(cohort, nextDay, weekBack, Percent(nextDay, cohort), Percent(weekBack, cohort)),
            Math.Round(avgDau, 1), wau, mau, mau == 0 ? 0 : Math.Round(100 * avgDau / mau, 1), since);
    }

    private static double Percent(int part, int total) => total == 0 ? 0 : Math.Round(100.0 * part / total, 1);
}
