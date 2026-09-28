using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Мета дня из боёв топ-игроков: колоды с итогами и пары «карта против карты».
///
/// Чистая функция без базы и сети - чтобы её можно было проверить отдельно:
/// ошибка здесь не падает, а тихо врёт в процентах побед, и заметить её по
/// экрану невозможно.
/// </summary>
public static class MetaAggregator
{
    /// <summary>
    /// Какие бои берём. Только соревновательные один на один: в дружеских, 2х2 и
    /// испытаниях колоды собирают под режим и под настроение, и мету они размывают.
    /// </summary>
    public static bool IsRanked(string? type) =>
        type is not null
        && (type.Equals("PvP", StringComparison.OrdinalIgnoreCase)
            || type.Equals("pathOfLegend", StringComparison.OrdinalIgnoreCase)
            || type.Equals("riverRacePvP", StringComparison.OrdinalIgnoreCase)
            // Новые рейтинговые режимы игра называет по-новому; «ranked» и
            // «legend» в названии - верный признак, что это тот же рейтинг.
            || type.Contains("ranked", StringComparison.OrdinalIgnoreCase)
            || type.Contains("legend", StringComparison.OrdinalIgnoreCase));

    public record DeckStat(int Games, int Wins, int Draws);
    public record PairStat(int Games, int Wins);

    /// <param name="Battles">Сколько уникальных боёв вошло после отсева дублей и режимов.</param>
    /// <param name="Games">Сами бои: колода против колоды и итог для стороны A.</param>
    public record Result(
        Dictionary<string, DeckStat> Decks,
        Dictionary<(int Card, int Opp), PairStat> Matchups,
        int Battles,
        List<(string DeckA, string DeckB, int Result)>? Games = null);

    /// <summary>
    /// Каждый бой даёт два наблюдения - по колоде с каждой стороны.
    ///
    /// Бой двух топ-игроков приходит дважды, из журнала каждого. Без отсева он
    /// засчитался бы двойным весом, и колоды, которыми топ играет против топа,
    /// выглядели бы вдвое популярнее. Ключ боя - время и оба тега по порядку:
    /// из какого журнала он пришёл, на ключ не влияет.
    /// </summary>
    public static Result Build(IEnumerable<CrRecentBattle> battles)
    {
        var decks = new Dictionary<string, (int G, int W, int D)>();
        var pairs = new Dictionary<(int, int), (int G, int W)>();
        var seen = new HashSet<string>();
        var games = new List<(string, string, int)>();
        var count = 0;

        foreach (var b in battles)
        {
            if (!IsRanked(b.Type)) continue;
            // Только один на один и полные колоды: в 2х2 по два тега с каждой стороны,
            // и «колода команды» там - две разные колоды двух людей.
            if (b.TeamTags.Count != 1 || b.OpponentTags.Count != 1) continue;
            if (b.MyDeck.Count != 8 || b.OpponentDeck.Count != 8) continue;

            var a = b.TeamTags[0];
            var o = b.OpponentTags[0];
            var key = string.CompareOrdinal(a, o) <= 0
                ? $"{b.BattleTimeUtc:O}|{a}|{o}"
                : $"{b.BattleTimeUtc:O}|{o}|{a}";
            if (!seen.Add(key)) continue;
            count++;

            var mine = b.MyDeck.Select(MetaCard.Key).ToArray();
            var theirs = b.OpponentDeck.Select(MetaCard.Key).ToArray();
            var draw = b.CrownsFor == b.CrownsAgainst;
            var iWon = !draw && b.Won;
            var theyWon = !draw && !b.Won;

            AddDeck(decks, MetaCard.DeckKey(mine), iWon, draw);
            AddDeck(decks, MetaCard.DeckKey(theirs), theyWon, draw);
            games.Add((MetaCard.DeckKey(mine), MetaCard.DeckKey(theirs), draw ? 0 : iWon ? 1 : -1));

            foreach (var c in mine)
                foreach (var t in theirs)
                {
                    AddPair(pairs, (c, t), iWon);
                    AddPair(pairs, (t, c), theyWon);
                }
        }

        return new Result(
            decks.ToDictionary(kv => kv.Key, kv => new DeckStat(kv.Value.G, kv.Value.W, kv.Value.D)),
            pairs.ToDictionary(kv => kv.Key, kv => new PairStat(kv.Value.G, kv.Value.W)),
            count,
            games);
    }

    private static void AddDeck(Dictionary<string, (int G, int W, int D)> map, string key, bool won, bool draw)
    {
        map.TryGetValue(key, out var v);
        map[key] = (v.G + 1, v.W + (won ? 1 : 0), v.D + (draw ? 1 : 0));
    }

    private static void AddPair(Dictionary<(int, int), (int G, int W)> map, (int, int) key, bool won)
    {
        map.TryGetValue(key, out var v);
        map[key] = (v.G + 1, v.W + (won ? 1 : 0));
    }

    /// <summary>
    /// Нижняя граница доверительного интервала Уилсона для доли побед.
    ///
    /// Сортировать колоды по голому проценту побед нельзя: колода с тремя играми и
    /// тремя победами обошла бы колоду с тысячей игр и 58%. Уилсон честно штрафует
    /// малую выборку - та же логика, по которой RoyaleAPI считает свой «рейтинг».
    /// </summary>
    public static double WilsonLower(int wins, int games, double z = 1.96)
    {
        if (games <= 0) return 0;
        var p = (double)wins / games;
        var denom = 1 + z * z / games;
        var centre = p + z * z / (2.0 * games);
        var margin = z * Math.Sqrt(p * (1 - p) / games + z * z / (4.0 * games * games));
        return (centre - margin) / denom;
    }
}
