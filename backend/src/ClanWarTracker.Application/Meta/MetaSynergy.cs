using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Связи между картами по реальным колодам топа за неделю: как часто две карты стоят в
/// одной колоде и как такие колоды выигрывают. Это и есть «синергия» конструктора - не
/// чьё-то мнение, а то, чем на самом деле играют лучшие.
///
/// Считается из недельного окна меты один раз и держится в памяти, пока окно то же.
/// </summary>
public static class MetaSynergy
{
    public sealed record Data(
        string WindowDay,
        Dictionary<int, (int Games, int Wins)> Cards,
        Dictionary<(int A, int B), (int Games, int Wins)> Pairs)
    {
        /// <summary>
        /// Как часто b играет вместе с a: доля игр колод с a, где есть и b (0..1).
        /// Ключ пары - по возрастанию id.
        /// </summary>
        public double CoPlay(int a, int b)
        {
            if (a == b || !Cards.TryGetValue(a, out var ca) || ca.Games == 0) return 0;
            return Pairs.TryGetValue(Key(a, b), out var p) ? (double)p.Games / ca.Games : 0;
        }

        public (int Games, int Wins) Pair(int a, int b) => Pairs.GetValueOrDefault(Key(a, b));
    }

    public static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    private static Data? _cached;
    private static readonly object Gate = new();

    public static Data For(MetaWindow.Data window)
    {
        var cached = _cached;
        if (cached is not null && cached.WindowDay == window.ToDayUtc + window.Decks.Count) return cached;
        lock (Gate)
        {
            cached = _cached;
            if (cached is not null && cached.WindowDay == window.ToDayUtc + window.Decks.Count) return cached;

            var cards = new Dictionary<int, (int Games, int Wins)>();
            var pairs = new Dictionary<(int, int), (int Games, int Wins)>();
            foreach (var d in window.Decks)
            {
                // Эволюция и обычная версия - одна карта: связь «Рыцарь + Golem» одна и та же
                var ids = MetaCard.ParseDeckKey(d.Key).Select(Math.Abs).Distinct().OrderBy(x => x).ToArray();
                if (ids.Length != 8) continue;
                foreach (var id in ids)
                {
                    var c = cards.GetValueOrDefault(id);
                    cards[id] = (c.Games + d.Games, c.Wins + d.Wins);
                }
                for (var i = 0; i < ids.Length; i++)
                    for (var j = i + 1; j < ids.Length; j++)
                    {
                        var p = pairs.GetValueOrDefault((ids[i], ids[j]));
                        pairs[(ids[i], ids[j])] = (p.Games + d.Games, p.Wins + d.Wins);
                    }
            }
            _cached = new Data(window.ToDayUtc + window.Decks.Count, cards, pairs);
            return _cached;
        }
    }
}
