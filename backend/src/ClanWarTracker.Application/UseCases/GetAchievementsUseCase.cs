using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Витрина наград игрока — считается из уже накопленных недельных снапшотов клана
/// (никаких новых таблиц). Пять значков, у каждого три уровня (бронза/серебро/золото):
///   🔥 streak       — недель войны подряд без пропуска
///   💯 perfectDays  — идеальные дни (900 медалей за день)
///   👑 mvpWeeks     — недель, где игрок был №1 клана по медалям
///   🏅 totalFame    — медали за всё время наблюдений
///   ⚔️ warsPlayed   — сыграно военных недель
/// </summary>
public class GetAchievementsUseCase(IWarSnapshotRepository snapshots)
{
    private const int WeeksWindow = 26; // полгода истории достаточно и дёшево

    private static readonly Dictionary<string, int[]> Levels = new()
    {
        ["streak"] = [3, 5, 10],
        ["dailyStreak"] = [3, 7, 15],
        ["perfectDays"] = [1, 5, 15],
        ["mvpWeeks"] = [1, 3, 8],
        ["totalFame"] = [10_000, 40_000, 100_000],
        ["warsPlayed"] = [3, 10, 25],
    };

    public async Task<AchievementsDto> ExecuteAsync(int clanId, string playerTag, CancellationToken ct = default)
    {
        var all = await snapshots.GetByClanAsync(clanId, WeeksWindow, ct);

        // Недели: финал = самый полный снимок (максимум славы клана); дни внутри недели — по PeriodIndex
        var weeks = all
            .GroupBy(s => (s.SeasonId, s.SectionIndex))
            .OrderBy(g => g.Key.SeasonId).ThenBy(g => g.Key.SectionIndex)
            .ToList();

        int totalFame = 0, warsPlayed = 0, mvpWeeks = 0, perfectDays = 0, streak = 0;
        var dayDecks = new List<int>(); // 4/4-дни в хронологии — для «серии дней»

        foreach (var week in weeks)
        {
            var final = week.OrderByDescending(s => s.TotalFame).ThenByDescending(s => s.PeriodIndex).First();
            var mine = final.Players.FirstOrDefault(p =>
                string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
            var myWeekFame = mine?.Fame ?? 0;

            totalFame += myWeekFame;
            if (myWeekFame > 0)
            {
                warsPlayed++;
                streak++; // серия считается по хронологии; пропуск недели её обнуляет
                var best = final.Players.Max(p => p.Fame);
                if (myWeekFame == best && best > 0) mvpWeeks++;
            }
            else
            {
                streak = 0;
            }

            // Идеальные дни + дневные колоды: дельты между соседними дневными снимками недели.
            // Колоды за день считаем той же дельтой (недельный DecksUsed растёт монотонно);
            // для первого военного дня берём суточный счётчик из снимка.
            var days = week.Where(s => s.PeriodIndex >= 3).OrderBy(s => s.PeriodIndex).ToList();
            int prevFame = 0, prevDecks = -1;
            foreach (var day in days)
            {
                var p = day.Players.FirstOrDefault(x =>
                    string.Equals(x.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
                var fame = p?.Fame ?? prevFame;
                if (fame - prevFame >= 900) perfectDays++;
                prevFame = fame;

                var decksToday = p is null ? 0
                    : prevDecks < 0 ? Math.Clamp(p.DecksUsedToday, 0, 4)
                    : Math.Clamp(p.DecksUsed - prevDecks, 0, 4);
                prevDecks = p?.DecksUsed ?? prevDecks;
                dayDecks.Add(decksToday);
            }
        }

        // Серия дней: подряд закрытых военных дней (4/4 колоды), считая с конца.
        // Сегодняшний (последний) день серию не рвёт, пока не доигран, — просто не входит в неё.
        var dailyStreak = 0;
        for (var i = dayDecks.Count - 1; i >= 0; i--)
        {
            if (dayDecks[i] >= 4) { dailyStreak++; continue; }
            if (i == dayDecks.Count - 1) continue; // текущий день ещё идёт
            break;
        }

        List<AchievementDto> badges =
        [
            Badge("streak", streak),
            Badge("dailyStreak", dailyStreak),
            Badge("perfectDays", perfectDays),
            Badge("mvpWeeks", mvpWeeks),
            Badge("totalFame", totalFame),
            Badge("warsPlayed", warsPlayed),
        ];

        return new AchievementsDto(playerTag, badges, weeks.Count);
    }

    private static AchievementDto Badge(string key, int value)
    {
        var t = Levels[key];
        var level = value >= t[2] ? 3 : value >= t[1] ? 2 : value >= t[0] ? 1 : 0;
        int? nextAt = level >= 3 ? null : t[level];
        return new AchievementDto(key, level, value, nextAt, t);
    }
}
