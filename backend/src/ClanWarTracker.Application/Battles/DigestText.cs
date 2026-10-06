using System.Globalization;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Тексты дневных итогов: утренний дайджест в личку («как прошло вчера») и вечерние
/// итоги клана в чат.
///
/// Чистые функции, как и <see cref="TrackerText"/>: use case только собирает бои и
/// отправляет, а что и в каком порядке сказать - решается здесь. Строки без данных
/// не пишем вовсе: «трёхкоронок: 0» - это шум, а не итог.
/// </summary>
public static class DigestText
{
    /// <summary>Хороший день: столько побед и боёв - вместо разбора поражений хвалим.</summary>
    public const double GreatDayWinRate = 0.7;
    public const int GreatDayMinBattles = 5;

    /// <summary>«Главное» - только если одному архетипу проиграно хотя бы дважды: одно поражение - не закономерность.</summary>
    public const int MainArchMinLosses = 2;

    /// <summary>Боец дня - не меньше пяти боёв: 2–0 за вечер ещё не подвиг.</summary>
    public const int FighterMinBattles = 5;

    /// <summary>Серия в итогах клана - от трёх побед: две подряд бывают у всех.</summary>
    public const int ClanStreakMin = 3;

    /// <summary>
    /// «14 боёв», «3 боя», «21 бой». Формы приходят из перевода через «|». Правило
    /// русское и украинское одно; у английского своё - там 21 тоже «battles».
    /// </summary>
    public static string Count(BotText t, int n, string forms)
    {
        var f = forms.Split('|');
        if (f.Length < 3) return $"{n} {forms}";
        int i;
        if (object.ReferenceEquals(t, BotText.En)) i = n == 1 ? 0 : 2;
        else
        {
            int m10 = n % 10, m100 = n % 100;
            i = m10 == 1 && m100 != 11 ? 0
                : m10 is >= 2 and <= 4 && (m100 < 12 || m100 > 14) ? 1
                : 2;
        }
        return $"{n} {f[i]}";
    }

    /// <summary>Самая длинная серия побед подряд; ничья серию обрывает.</summary>
    public static int BestWinStreak(IEnumerable<PlayerBattle> battles)
    {
        int best = 0, run = 0;
        foreach (var b in battles)
        {
            run = b.Result > 0 ? run + 1 : 0;
            best = Math.Max(best, run);
        }
        return best;
    }

    public static bool ThreeCrown(PlayerBattle b) => b.Result > 0 && b.CrownsFor >= 3;

    /// <summary>Сумма кубков; null - API ни в одном бою их не прислал (Путь легенд, турниры).</summary>
    public static int? TrophySum(IReadOnlyCollection<PlayerBattle> battles) =>
        battles.Any(b => b.TrophyChange is not null) ? battles.Sum(b => b.TrophyChange ?? 0) : null;

    /// <summary>Утренний дайджест: итог дня, серия и трёхкоронки, причины поражений, главное или похвала.</summary>
    /// <param name="day">Учтённые бои вчерашнего дня (ладдер и Путь легенд) по возрастанию времени.</param>
    /// <param name="history">Те же бои и немного до них - чтобы серия поражений с вечера не рвалась на полуночи.</param>
    /// <param name="top">Матчап боя по топ-500 или null.</param>
    public static string Morning(
        BotText t, IReadOnlyList<PlayerBattle> day, IReadOnlyList<PlayerBattle> history,
        Func<PlayerBattle, MatchupStats.Result?> top, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var wins = day.Count(b => b.Result > 0);
        var losses = day.Where(b => b.Result < 0).ToList();
        var trophies = TrophySum(day);

        var lines = new List<string>
        {
            string.Format(t.DgHead, Count(t, day.Count, t.FormsBattles), wins, losses.Count,
                trophies is int tr ? $" · {TiltMessages.Signed(tr)} 🏆" : ""),
        };

        var parts = new List<string>();
        var streak = BestWinStreak(day);
        if (streak >= 2) parts.Add(string.Format(t.DgStreak, Count(t, streak, t.FormsWins)));
        var crowns = day.Count(ThreeCrown);
        if (crowns > 0) parts.Add(string.Format(t.DgCrowns, Count(t, crowns, t.FormsThreeCrowns)));
        if (parts.Count > 0) lines.Add(string.Join(" · ", parts));

        if (losses.Count > 0)
        {
            var reasons = losses
                .Select(b => LossReasons.Classify(b, history, top(b))?.Main ?? LossReasons.Outplayed)
                .GroupBy(r => r)
                .OrderBy(g => Array.IndexOf(LossReasons.Priority, g.Key))
                .Select(g => $"{TrackerText.LossLabel(t, g.Key)} — {g.Count()}");
            lines.Add(string.Format(t.DgReasons, string.Join(", ", reasons)));
        }

        // Удачный день - хвалим, а не ищем, к чему придраться: утро должно звать играть.
        var rate = day.Count == 0 ? 0 : (double)wins / day.Count;
        if (day.Count >= GreatDayMinBattles && rate >= GreatDayWinRate)
        {
            lines.Add(string.Format(t.DgPraise, Math.Round(rate * 100).ToString("0", CultureInfo.InvariantCulture)));
        }
        else
        {
            var worst = losses
                .GroupBy(b => MatchReport.ArchOf(b, catalog))
                .Where(g => g.Key != "" && g.Count() >= MainArchMinLosses)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (worst is not null)
                lines.Add(string.Format(t.DgMain, worst.Count(), losses.Count, TrackerText.ArchLabelOf(t, worst.Key, catalog)));
        }

        return string.Join("\n", lines);
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> MorningButtons(BotText t) =>
    [
        [new BotButton(t.DgBtnReview, Url: "startapp:review"), new BotButton(t.DgBtnDuel, SwitchInline: "duel")],
        [new BotButton(t.DgBtnOff, DigestOffCallback)],
    ];

    public const string DigestOffCallback = "dg|off";

    /// <param name="Trophies">Сумма кубков за день; null - кубков в боях не было.</param>
    public record PlayerDay(string Name, int Battles, int Wins, int Losses, int? Trophies, int BestStreak, int ThreeCrowns);

    /// <summary>Итоги одного игрока за день по его учтённым боям (по возрастанию времени).</summary>
    public static PlayerDay Summarize(string name, IReadOnlyList<PlayerBattle> battles) => new(
        name, battles.Count, battles.Count(b => b.Result > 0), battles.Count(b => b.Result < 0),
        TrophySum(battles), BestWinStreak(battles), battles.Count(ThreeCrown));

    /// <summary>Итоги дня клана. Вызывающий сам решает, хватает ли игроков, чтобы писать.</summary>
    public static string ClanRecap(BotText t, string clanName, IReadOnlyList<PlayerDay> players)
    {
        var lines = new List<string> { string.Format(t.RcHead, clanName) };

        // Боец дня - больше всех побед; при равенстве - кто реже проигрывал.
        var fighter = players
            .Where(p => p.Battles >= FighterMinBattles && p.Wins > 0)
            .OrderByDescending(p => p.Wins)
            .ThenByDescending(p => (double)p.Wins / p.Battles)
            .FirstOrDefault();
        if (fighter is not null)
            lines.Add(string.Format(t.RcFighter, fighter.Name, fighter.Wins, fighter.Losses,
                fighter.Trophies is int ft ? $" ({TiltMessages.Signed(ft)} 🏆)" : ""));

        var streak = players.OrderByDescending(p => p.BestStreak).FirstOrDefault();
        if (streak is not null && streak.BestStreak >= ClanStreakMin)
            lines.Add(string.Format(t.RcStreak, streak.Name, Count(t, streak.BestStreak, t.FormsWins)));

        var crowns = players.OrderByDescending(p => p.ThreeCrowns).FirstOrDefault();
        if (crowns is not null && crowns.ThreeCrowns > 0)
            lines.Add(string.Format(t.RcCrowns, crowns.Name, crowns.ThreeCrowns));

        var climber = players
            .Where(p => p.Trophies is > 0)
            .OrderByDescending(p => p.Trophies)
            .FirstOrDefault();
        if (climber is not null)
            lines.Add(string.Format(t.RcTrophies, climber.Name, TiltMessages.Signed(climber.Trophies!.Value)));

        var battles = players.Sum(p => p.Battles);
        var pct = battles == 0 ? 0 : Math.Round(100.0 * players.Sum(p => p.Wins) / battles);
        lines.Add(string.Format(t.RcTotal, Count(t, players.Count, t.FormsPlayers), Count(t, battles, t.FormsBattles),
            pct.ToString("0", CultureInfo.InvariantCulture)));

        return string.Join("\n", lines);
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> ClanRecapButtons(BotText t) =>
        [[new BotButton(t.RcBtn, Url: "startapp:review")]];
}
