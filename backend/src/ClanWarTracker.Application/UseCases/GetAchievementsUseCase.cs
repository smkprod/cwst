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

    /// <summary>4 атаки без поражений — максимум за военный день.</summary>
    private const int PerfectDayFame = 900;

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
            // Тот же приоритет, что и в сезонном зачёте: подтверждённый журналом финал важнее живого
            var final = week
                .OrderByDescending(s => s.Source == "log" && s.TotalFame > 0)
                .ThenByDescending(s => s.TotalFame)
                .ThenByDescending(s => s.PeriodIndex)
                .First();
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

            // Идеальные дни + дневные колоды: дельты между СОСЕДНИМИ дневными снимками.
            // Считаем только там, где база достоверна: либо это первый военный день
            // (накопленное за неделю = за день), либо предыдущий день реально снят.
            // Иначе пропуск снимка превратил бы недельную славу в один «идеальный день»
            // — ровно та ошибка, из-за которой бот ложно поздравлял с 900.
            var days = week.Where(s => s.PeriodIndex is >= 3 and <= 6)
                .GroupBy(s => s.PeriodIndex)
                .Select(g => g.OrderByDescending(s => s.TotalFame).First())
                .OrderBy(s => s.PeriodIndex)
                .ToList();

            int? prevFame = null, prevDecks = null;
            var prevPeriod = -1;
            foreach (var day in days)
            {
                var p = day.Players.FirstOrDefault(x =>
                    string.Equals(x.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));

                var isFirstWarDay = day.PeriodIndex == 3;
                var hasBaseline = isFirstWarDay || (day.PeriodIndex == prevPeriod + 1 && prevFame is not null);

                if (p is not null && hasBaseline)
                {
                    var baseFame = isFirstWarDay ? 0 : prevFame!.Value;
                    // Ровно максимум: меньше — не идеальный день, больше физически нельзя
                    // (значит база всё-таки врёт, и засчитывать нечего).
                    if (p.Fame - baseFame == PerfectDayFame) perfectDays++;

                    var decksToday = isFirstWarDay || prevDecks is null
                        ? Math.Clamp(p.DecksUsedToday, 0, 4)
                        : Math.Clamp(p.DecksUsed - prevDecks.Value, 0, 4);
                    dayDecks.Add(decksToday);
                }

                if (p is not null) { prevFame = p.Fame; prevDecks = p.DecksUsed; }
                prevPeriod = day.PeriodIndex;
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
