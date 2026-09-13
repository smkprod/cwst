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
///   💎 perfectWeeks — идеальные недели (3600 медалей: 16 атак без единого поражения)
///   🏆 perfectSeasons — сезоны, где отыграны все колоды во всех неделях
///   🚤 boatAttacks  — атаки по лодке за всё время
/// </summary>
public class GetAchievementsUseCase(IWarSnapshotRepository snapshots)
{
    private const int WeeksWindow = 26; // полгода истории достаточно и дёшево

    /// <summary>4 атаки без поражений — максимум за военный день.</summary>
    private const int PerfectDayFame = 900;

    /// <summary>
    /// Потолок недели: 16 атак по 225. Одинаков и для обычной войны (4 дня × 4),
    /// и для колизея (все 16 в любой день) — поэтому одна константа на оба формата.
    /// </summary>
    private const int PerfectWeekFame = 3600;

    /// <summary>Сколько колод нужно отыграть за неделю, чтобы она считалась закрытой.</summary>
    private const int WarDecksPerWeek = 16;

    /// <summary>
    /// Меньше трёх недель — это не сезон, а его огрызок. Бот мог подключиться к клану
    /// в середине, и объявлять такой хвост «идеальным сезоном» значило бы выдавать
    /// награду за то, что мы просто поздно начали смотреть.
    /// </summary>
    private const int MinWeeksForSeason = 3;

    private static readonly Dictionary<string, int[]> Levels = new()
    {
        ["streak"] = [3, 5, 10],
        ["dailyStreak"] = [3, 7, 15],
        ["perfectDays"] = [1, 5, 15],
        ["mvpWeeks"] = [1, 3, 8],
        ["totalFame"] = [10_000, 40_000, 100_000],
        ["warsPlayed"] = [3, 10, 25],
        ["perfectWeeks"] = [1, 3, 8],
        ["perfectSeasons"] = [1, 2, 4],
        ["boatAttacks"] = [5, 25, 75],
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
        int perfectWeeks = 0, boatAttacks = 0;
        var dayDecks = new List<int>(); // 4/4-дни в хронологии — для «серии дней»

        // Сезон → (сколько недель видели, во всех ли отыграны все колоды)
        var seasons = new Dictionary<int, (int Weeks, bool AllFull)>();

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
            boatAttacks += mine?.BoatAttacks ?? 0;
            if (myWeekFame == PerfectWeekFame) perfectWeeks++;

            // Сезон закрыт идеально, только если КАЖДАЯ его неделя отыграна полностью.
            // Одна пропущенная неделя рушит весь сезон — в этом и смысл награды.
            var weeksSoFar = 0;
            var allFullSoFar = true;
            if (seasons.TryGetValue(final.SeasonId, out var seenSeason))
            {
                weeksSoFar = seenSeason.Weeks;
                allFullSoFar = seenSeason.AllFull;
            }

            seasons[final.SeasonId] = (
                weeksSoFar + 1,
                allFullSoFar && (mine?.DecksUsed ?? 0) >= WarDecksPerWeek);

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

        var perfectSeasons = seasons.Count(s => s.Value.Weeks >= MinWeeksForSeason && s.Value.AllFull);

        List<AchievementDto> badges =
        [
            Badge("streak", streak),
            Badge("dailyStreak", dailyStreak),
            Badge("perfectDays", perfectDays),
            Badge("mvpWeeks", mvpWeeks),
            Badge("totalFame", totalFame),
            Badge("warsPlayed", warsPlayed),
            Badge("perfectWeeks", perfectWeeks),
            Badge("perfectSeasons", perfectSeasons),
            Badge("boatAttacks", boatAttacks),
        ];

        return new AchievementsDto(playerTag, badges, weeks.Count, JustUnlocked: []);
    }

    /// <summary>
    /// Что открылось с прошлого просмотра, и каким стал снимок уровней.
    ///
    /// Уровень — это и есть «ачивка, которую получают один раз»: бронза берётся
    /// однажды, серебро однажды, золото однажды. Не хватало только момента — без
    /// сравнения со снимком человек просто однажды замечает, что число выросло.
    ///
    /// Незнакомые ключи в старом снимке игнорируем: список наград пополняется, и
    /// падать на снимке, снятом до появления значка, нельзя. Новый значок, у
    /// которого сразу есть уровень, считается открытым — это честно, человек его
    /// и правда только что увидел впервые.
    /// </summary>
    public static (List<string> Unlocked, string Snapshot) Diff(
        IEnumerable<AchievementDto> badges, string? seenJson)
    {
        Dictionary<string, int> seen;
        try
        {
            seen = string.IsNullOrWhiteSpace(seenJson)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(seenJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            // Снимок испорчен — начинаем заново. Поздравить лишний раз не страшно,
            // уронить витрину из-за кривой строки в базе — страшно.
            seen = [];
        }

        var now = badges.ToDictionary(b => b.Key, b => b.Level);
        var unlocked = now
            .Where(b => b.Value > 0 && b.Value > seen.GetValueOrDefault(b.Key))
            .Select(b => b.Key)
            .ToList();

        return (unlocked, System.Text.Json.JsonSerializer.Serialize(now));
    }

    private static AchievementDto Badge(string key, int value)
    {
        var t = Levels[key];
        var level = value >= t[2] ? 3 : value >= t[1] ? 2 : value >= t[0] ? 1 : 0;
        int? nextAt = level >= 3 ? null : t[level];
        return new AchievementDto(key, level, value, nextAt, t);
    }
}
