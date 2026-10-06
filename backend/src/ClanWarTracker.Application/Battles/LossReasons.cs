using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// «Почему проиграл»: одна главная причина поражения и, если есть, второстепенные.
///
/// Только из того, что у нас реально есть: уровни, статистика матчапа в топ-500,
/// серия поражений, утечка эликсира, счёт. Ходов не видно, поэтому, когда ни одна
/// причина не подтверждается, честно говорим «переиграли», а не выдумываем.
///
/// Чистая функция: её зовут и карточка трекера, и история боёв в приложении, и
/// утренний дайджест - причина одного и того же боя везде звучит одинаково.
/// </summary>
public static class LossReasons
{
    public const string Levels = "levels";
    public const string Matchup = "matchup";
    public const string Tilt = "tilt";
    public const string Leak = "leak";
    public const string Close = "close";
    public const string Outplayed = "outplayed";

    /// <summary>Порядок важности: главная причина - первая сработавшая. Он же - порядок строк в дайджесте.</summary>
    public static readonly string[] Priority = [Levels, Matchup, Tilt, Leak, Close, Outplayed];

    /// <summary>
    /// Карты соперника выше в среднем хотя бы на полуровня. Меньше - уже шум:
    /// одна недокачанная карта сдвигает среднее на 0.1–0.2 и боя не решает.
    /// </summary>
    public const double LevelGapMax = -0.5;

    /// <summary>
    /// Матчап плохой, если в топ-500 такие колоды против таких выигрывают не больше 42%.
    /// Около 50% - монетка; восемь пунктов ниже - уже заметный перекос, который
    /// чувствуется и у игроков сильнее тебя.
    /// </summary>
    public const double MatchupMaxPercent = 42;

    /// <summary>
    /// Тильт - поражение сразу после двух поражений подряд в том же заходе. Два слива
    /// случаются со всеми; третий подряд - это уже игра на эмоциях, а не выбор.
    /// </summary>
    public const int TiltLossesBefore = 2;

    /// <summary>
    /// Утекло 4 эликсира и больше - заметно: это две карты, которые так и не вышли.
    /// АФК (15+) сюда тоже попадает - причина та же, просто громче.
    /// </summary>
    public const double LeakMin = 4.0;

    /// <param name="Main">Главная причина - один из ключей <see cref="Priority"/>.</param>
    /// <param name="Secondary">Остальные подтвердившиеся причины, по важности (без close/outplayed).</param>
    /// <param name="LevelGap">Свой средний уровень минус у соперника (отрицательное - соперник выше).</param>
    /// <param name="MatchupPercent">% побед таких колод против таких в топ-500, если матчап плохой.</param>
    /// <param name="LossInRow">Каким по счёту подряд было это поражение в заходе (1 - первое).</param>
    /// <param name="Leak">Утечка эликсира в этом бою.</param>
    public record Reason(
        string Main, IReadOnlyList<string> Secondary, double? LevelGap, double? MatchupPercent, int LossInRow, double? Leak);

    /// <param name="b">Бой. Не поражение - null.</param>
    /// <param name="history">Бои игрока по возрастанию времени (могут включать и сам бой, и более поздние).</param>
    /// <param name="top">Матчап по боям топа (<see cref="MatchupStats.FromTop"/>); null - данных нет.</param>
    public static Reason? Classify(PlayerBattle b, IReadOnlyList<PlayerBattle> history, MatchupStats.Result? top)
    {
        if (b.Result >= 0) return null;

        // Порядок добавления совпадает с приоритетом: первая сработавшая - главная.
        var found = new List<string>(4);
        if (b.LevelGap is double gap && gap <= LevelGapMax) found.Add(Levels);
        var matchup = BadMatchupPercent(top);
        if (matchup is not null) found.Add(Matchup);
        var before = LossesBefore(b, history);
        if (before >= TiltLossesBefore) found.Add(Tilt);
        if (b.ElixirLeaked is double leak && leak >= LeakMin) found.Add(Leak);

        string main;
        if (found.Count > 0) main = found[0];
        // Минус одна корона без других причин - бой, который мог уйти в любую сторону.
        else if (b.CrownsAgainst - b.CrownsFor == 1) main = Close;
        else main = Outplayed;

        return new Reason(main, found.Skip(1).ToList(), b.LevelGap, matchup, before + 1, b.ElixirLeaked);
    }

    /// <summary>% побед в топ-500, если матчап плохой и выборка надёжна; иначе null.</summary>
    public static double? BadMatchupPercent(MatchupStats.Result? top)
    {
        if (top?.Headline is not { } h) return null;
        if (h.Reliability == "low" || h.WinPercent > MatchupMaxPercent) return null;
        return h.WinPercent;
    }

    /// <summary>
    /// Сколько поражений подряд было прямо перед этим боем в том же заходе. Считаем,
    /// как «Стоп-тильт»: только ладдер и Путь легенд, ничья серию не рвёт и не
    /// продолжает, перерыв больше <see cref="BattleAnalyzer.SessionGap"/> - новый заход.
    /// </summary>
    public static int LossesBefore(PlayerBattle b, IReadOnlyList<PlayerBattle> history)
    {
        // Война и турниры - не про тильт: там другая ставка и чужие правила.
        if (!BattleAnalyzer.IsTiltEligible(b.Type)) return 0;

        var streak = 0;
        var later = b.BattleTimeUtc;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var x = history[i];
            if (x.BattleTimeUtc >= b.BattleTimeUtc || !BattleAnalyzer.IsTiltEligible(x.Type)) continue;
            if (later - x.BattleTimeUtc > BattleAnalyzer.SessionGap) break;
            if (x.Result > 0) break;
            if (x.Result < 0) streak++;
            later = x.BattleTimeUtc;
        }
        return streak;
    }
}
