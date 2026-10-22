using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Итог игрового дня одной картинкой: счёт, кубки, лучшая серия, три короны и цепочка
/// боёв по порядку. Ладдер и Путь легенд - как в утренней сводке: война и турниры
/// живут по своим правилам.
/// </summary>
/// <param name="Results">Исходы боёв по времени: 1 - победа, 0 - ничья, -1 - поражение.</param>
public record DayRecap(int Wins, int Losses, int Draws, int Trophies, int BestStreak, int ThreeCrowns, List<int> Results)
{
    public int Battles => Results.Count;
    public int WinPercent => Battles == 0 ? 0 : (int)Math.Round(100.0 * Wins / Battles);

    /// <summary>Больше на картинку не влезает - рисуем последние.</summary>
    public const int MaxResultsShown = 30;

    public static DayRecap Build(IEnumerable<PlayerBattle> battles)
    {
        int wins = 0, losses = 0, draws = 0, trophies = 0, streak = 0, best = 0, three = 0;
        var results = new List<int>();
        foreach (var b in battles.Where(b => MatchReport.ModeKey(b.Type) is "ladder" or "pol").OrderBy(b => b.BattleTimeUtc))
        {
            var r = Math.Sign(b.Result);
            results.Add(r);
            trophies += b.TrophyChange ?? 0;
            if (r > 0)
            {
                wins++;
                best = Math.Max(best, ++streak);
                if (b.CrownsFor >= 3) three++;
            }
            else
            {
                streak = 0;
                if (r < 0) losses++; else draws++;
            }
        }
        return new DayRecap(wins, losses, draws, trophies, best, three, results);
    }

    /// <summary>Границы местного дня: back 0 - сегодня, 1 - вчера.</summary>
    public static (DateTime FromUtc, DateTime ToUtc, DateTime LocalDate) Window(DateTime nowUtc, int tzOffsetMinutes, int back)
    {
        var todayStart = TiltMessages.LocalMidnightUtc(nowUtc, tzOffsetMinutes);
        var from = todayStart.AddDays(-back);
        return (from, from.AddDays(1), nowUtc.AddMinutes(tzOffsetMinutes).Date.AddDays(-back));
    }

    public static async Task<DayRecap> LoadAsync(IPlayerBattleRepository battles, string playerTag,
        DateTime nowUtc, int tzOffsetMinutes, int back, CancellationToken ct = default)
    {
        var (from, to, _) = Window(nowUtc, tzOffsetMinutes, back);
        var list = (await battles.GetSinceAsync(playerTag, from, ct)).Where(b => b.BattleTimeUtc < to);
        return Build(list);
    }
}
