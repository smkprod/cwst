namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Против каких карт колода проседает - по парам «карта против карты» из боёв топа.
///
/// Прямой статистики «колода против колоды» нет: пары колод почти все встречаются
/// по разу. Зато у каждой из восьми карт есть сотни встреч с любой картой соперника.
/// Складываем встречи всех восьми карт с картой X и сравниваем с тем, как эти же
/// восемь карт играют против всех подряд. Разница и есть «насколько X мешает»:
/// голый процент против X ничего бы не сказал - слабая колода проигрывает всем.
/// </summary>
public static class MetaCounters
{
    /// <param name="OppKey">Ключ карты соперника (минус - эволюция).</param>
    /// <param name="WinRate">Доля побед колоды, когда у соперника есть эта карта, 0..1.</param>
    /// <param name="Delta">Насколько это ниже обычного для колоды, в долях (отрицательное - контра).</param>
    public record Counter(int OppKey, double WinRate, double Delta, int Games);

    /// <summary>Пары за окно, сложенные по дням: (карта, карта соперника) → игры и победы.</summary>
    public static Dictionary<(int Card, int Opp), (int Games, int Wins)> Sum(
        IEnumerable<(int Card, int Opp, int Games, int Wins)> rows)
    {
        var map = new Dictionary<(int, int), (int Games, int Wins)>();
        foreach (var r in rows)
        {
            map.TryGetValue((r.Card, r.Opp), out var v);
            map[(r.Card, r.Opp)] = (v.Games + r.Games, v.Wins + r.Wins);
        }
        return map;
    }

    /// <summary>
    /// Худшие для колоды карты соперника, от самой неудобной.
    /// </summary>
    /// <param name="minGames">
    /// Порог встреч по сумме восьми карт. Ниже - разница больше похожа на шум,
    /// чем на контру, и советовать по ней «бойся Молнии» было бы гаданием.
    /// </param>
    public static List<Counter> For(
        IReadOnlyCollection<int> deck,
        Dictionary<(int Card, int Opp), (int Games, int Wins)> pairs,
        int take = 3,
        int minGames = 40)
    {
        var mine = deck.ToHashSet();
        var byOpp = new Dictionary<int, (int Games, int Wins)>();
        long allGames = 0, allWins = 0;

        foreach (var ((card, opp), v) in pairs)
        {
            if (!mine.Contains(card)) continue;
            allGames += v.Games;
            allWins += v.Wins;
            // Карты, которые есть в самой колоде, не считаем: «контра Хогу - Хог»
            // это зеркало, а не совет.
            if (mine.Contains(opp)) continue;
            byOpp.TryGetValue(opp, out var s);
            byOpp[opp] = (s.Games + v.Games, s.Wins + v.Wins);
        }

        if (allGames == 0) return [];
        var baseline = (double)allWins / allGames;

        return byOpp
            .Where(kv => kv.Value.Games >= minGames)
            .Select(kv =>
            {
                var rate = (double)kv.Value.Wins / kv.Value.Games;
                return new Counter(kv.Key, rate, rate - baseline, kv.Value.Games);
            })
            .Where(c => c.Delta < 0)
            .OrderBy(c => c.Delta)
            .Take(take)
            .ToList();
    }
}
