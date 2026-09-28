using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// «Как такие колоды играют друг против друга»: лестница похожести от точного
/// совпадения до «тот же вин-кон».
///
/// Точная пара колод встречается редко, поэтому идём ступенями: чем мягче условие,
/// тем больше боёв и тем меньше они про именно эти колоды. Рядом с каждой ступенью -
/// сколько боёв в ней, чтобы 3 из 4 не читались как закон.
///
/// Эволюцию не различаем: для «та же колода или нет» Эво-Рыцарь - это Рыцарь.
/// </summary>
public static class MatchupStats
{
    /// <summary>Ступени от самой точной к самой общей.</summary>
    public static readonly string[] Tiers = ["exact", "seven", "six", "wincon4", "wincon"];

    /// <summary>Боёв на ступени, чтобы считать её надёжной и хотя бы годной.</summary>
    public const int Reliable = 100;
    public const int Adequate = 20;

    public record Tier(string Key, int Wins, int Draws, int Losses)
    {
        public int Games => Wins + Draws + Losses;
        public double WinPercent => Games == 0 ? 0 : Math.Round(100.0 * Wins / Games, 1);
        public string Reliability => Games >= Reliable ? "reliable" : Games >= Adequate ? "adequate" : "low";
    }

    /// <param name="Headline">Самая точная ступень, где боёв хотя бы <see cref="Adequate"/>; null - такой нет.</param>
    public record Result(IReadOnlyList<Tier> Tiers, Tier? Headline);

    /// <summary>Вин-кон колоды - главная карта её архетипа. null - архетип не определился.</summary>
    public static int? WinCon(string deckKey, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var key = Archetypes.ClassifyKey(deckKey, catalog);
        return key is null ? null : Archetypes.KeyCard(key, catalog);
    }

    /// <summary>По боям топа. Каждый бой смотрим с обеих сторон: колода могла быть и A, и B.</summary>
    public static Result FromTop(string myDeck, string oppDeck, MatchupWindow.Data data, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var acc = new Acc(MatchupWindow.Ids(myDeck), MatchupWindow.Ids(oppDeck), WinCon(myDeck, catalog), WinCon(oppDeck, catalog));
        var cards = data.Cards;
        for (var i = 0; i < data.Count; i++)
        {
            var a = new ReadOnlySpan<int>(cards, i * 16, 8);
            var b = new ReadOnlySpan<int>(cards, i * 16 + 8, 8);
            int r = data.Results[i];
            acc.Add(a, b, r);
            acc.Add(b, a, -r);
        }
        return acc.Finish();
    }

    /// <summary>По своим боям: моя колода против колоды соперника, итог уже с моей стороны.</summary>
    public static Result FromOwn(string myDeck, string oppDeck, IEnumerable<PlayerBattle> battles, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var acc = new Acc(MatchupWindow.Ids(myDeck), MatchupWindow.Ids(oppDeck), WinCon(myDeck, catalog), WinCon(oppDeck, catalog));
        foreach (var b in battles)
        {
            var mine = MatchupWindow.Ids(b.DeckKey);
            var theirs = MatchupWindow.Ids(b.OppDeckKey);
            if (mine.Length != 8 || theirs.Length != 8) continue;
            acc.Add(mine, theirs, b.Result);
        }
        return acc.Finish();
    }

    /// <summary>Сколько общих карт у двух отсортированных восьмёрок.</summary>
    public static int Shared(ReadOnlySpan<int> x, ReadOnlySpan<int> y)
    {
        int i = 0, j = 0, n = 0;
        while (i < x.Length && j < y.Length)
        {
            if (x[i] == y[j]) { n++; i++; j++; }
            else if (x[i] < y[j]) i++;
            else j++;
        }
        return n;
    }

    private sealed class Acc(int[] me, int[] opp, int? meWc, int? oppWc)
    {
        private readonly int[] _w = new int[5], _d = new int[5], _l = new int[5];

        public void Add(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int result)
        {
            // «Только вин-кон» не требует общих карт - считаем до отсечки по ним.
            var wc = meWc is int mw && oppWc is int ow && x.Contains(mw) && y.Contains(ow);
            Count(4, wc, result);

            var mm = Shared(me, x);
            if (mm < 4) return;                     // остальным ступеням нужно хотя бы 4 общие карты
            var oo = Shared(opp, y);
            if (oo < 4) return;

            Count(0, mm == 8 && oo == 8, result);
            Count(1, mm >= 7 && oo >= 7, result);
            Count(2, mm >= 6 && oo >= 6, result);
            Count(3, wc, result);
        }

        private void Count(int tier, bool hit, int result)
        {
            if (!hit) return;
            if (result > 0) _w[tier]++;
            else if (result < 0) _l[tier]++;
            else _d[tier]++;
        }

        public Result Finish()
        {
            var tiers = Tiers.Select((k, i) => new Tier(k, _w[i], _d[i], _l[i])).ToList();
            var headline = tiers.FirstOrDefault(t => t.Games >= Adequate);
            return new Result(tiers, headline);
        }
    }
}
