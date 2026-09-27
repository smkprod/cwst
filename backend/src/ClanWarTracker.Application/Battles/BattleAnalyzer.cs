using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Разбор боёв одного игрока: чистые функции без базы и сети.
///
/// Всё здесь считается по сохранённым боям, отсортированным по времени. Отдельно от
/// use case - чтобы логику можно было проверить на выдуманных боях: ошибка в ней
/// не падает, а тихо врёт человеку про его игру.
/// </summary>
public static class BattleAnalyzer
{
    /// <summary>
    /// Бои дальше этого друг от друга - разные заходы. Тильт живёт внутри одного
    /// захода: два поражения вчера вечером не портят первый бой сегодня утром.
    /// </summary>
    public static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(30);

    public record Totals(int Games, int Wins, int Losses, int Draws);

    public static Totals Count(IReadOnlyList<PlayerBattle> battles) => new(
        battles.Count,
        battles.Count(b => b.Result > 0),
        battles.Count(b => b.Result < 0),
        battles.Count(b => b.Result == 0));

    public static double Percent(int wins, int games) => games == 0 ? 0 : Math.Round(100.0 * wins / games, 1);

    public record Leak(double Wins, double Losses, double All, int Games);

    /// <summary>Средняя утечка эликсира в победах и поражениях. null - API её не присылал.</summary>
    public static Leak? ElixirLeak(IReadOnlyList<PlayerBattle> battles)
    {
        var known = battles.Where(b => b.ElixirLeaked is not null).ToList();
        if (known.Count == 0) return null;
        static double Avg(IEnumerable<PlayerBattle> xs) =>
            xs.Select(b => b.ElixirLeaked!.Value).DefaultIfEmpty(0).Average();
        return new Leak(
            Math.Round(Avg(known.Where(b => b.Result > 0)), 2),
            Math.Round(Avg(known.Where(b => b.Result < 0)), 2),
            Math.Round(Avg(known), 2),
            known.Count);
    }

    public record Slot(string Key, int Games, int Wins);

    /// <summary>
    /// Части суток по местному времени игрока. Смещение приходит из приложения:
    /// «вечером» у игрока из Владивостока и из Калининграда - разные часы UTC.
    /// </summary>
    public static List<Slot> DayParts(IReadOnlyList<PlayerBattle> battles, int tzOffsetMinutes)
    {
        string Part(DateTime utc) => utc.AddMinutes(tzOffsetMinutes).Hour switch
        {
            < 6 => "night",
            < 12 => "morning",
            < 18 => "day",
            _ => "evening",
        };
        var order = new[] { "morning", "day", "evening", "night" };
        return order
            .Select(k =>
            {
                var xs = battles.Where(b => Part(b.BattleTimeUtc) == k).ToList();
                return new Slot(k, xs.Count, xs.Count(b => b.Result > 0));
            })
            .ToList();
    }

    /// <summary>Дни недели по местному времени: 0 - понедельник, 6 - воскресенье.</summary>
    public static List<Slot> Weekdays(IReadOnlyList<PlayerBattle> battles, int tzOffsetMinutes)
    {
        int Day(DateTime utc) => ((int)utc.AddMinutes(tzOffsetMinutes).DayOfWeek + 6) % 7;
        return Enumerable.Range(0, 7)
            .Select(d =>
            {
                var xs = battles.Where(b => Day(b.BattleTimeUtc) == d).ToList();
                return new Slot(d.ToString(), xs.Count, xs.Count(b => b.Result > 0));
            })
            .ToList();
    }

    public record TiltStats(int AfterTwoLosses, int AfterTwoLossesWins, int AfterWin, int AfterWinWins, int LongestLossStreak);

    /// <summary>
    /// Как играешь сразу после двух поражений подряд - и для сравнения после победы.
    /// Бои считаются подряд, только если между ними меньше <see cref="SessionGap"/>.
    /// Ничья серию не рвёт и не продолжает: её просто пропускаем.
    /// </summary>
    public static TiltStats Tilt(IReadOnlyList<PlayerBattle> battles)
    {
        int after2 = 0, after2Wins = 0, afterWin = 0, afterWinWins = 0;
        int streak = 0, longest = 0, lossRun = 0;
        DateTime? last = null;

        foreach (var b in battles)
        {
            if (last is { } prev && b.BattleTimeUtc - prev > SessionGap)
                streak = 0;   // новый заход - счёт поражений с нуля

            // streak > 0 - столько поражений подряд перед этим боем; < 0 - перед ним победа
            if (streak >= 2)
            {
                after2++;
                if (b.Result > 0) after2Wins++;
            }
            else if (streak < 0)
            {
                afterWin++;
                if (b.Result > 0) afterWinWins++;
            }

            if (b.Result < 0)
            {
                streak = streak > 0 ? streak + 1 : 1;
                lossRun++;
                longest = Math.Max(longest, lossRun);
            }
            else if (b.Result > 0)
            {
                streak = -1;
                lossRun = 0;
            }

            last = b.BattleTimeUtc;
        }

        return new TiltStats(after2, after2Wins, afterWin, afterWinWins, longest);
    }

    public record DeckUse(string Key, int Games, int Wins);

    /// <summary>Свои колоды по числу игр.</summary>
    public static List<DeckUse> Decks(IReadOnlyList<PlayerBattle> battles) =>
        battles
            .GroupBy(b => b.DeckKey)
            .Select(g => new DeckUse(g.Key, g.Count(), g.Count(b => b.Result > 0)))
            .OrderByDescending(d => d.Games)
            .ToList();

    public record Tough(int CardKey, int Games, int Wins, double Delta);

    /// <summary>
    /// Карты соперника, против которых твой процент побед ниже твоего обычного.
    /// Сравниваем с собой, а не с 50%: игроку, который выигрывает 40%, «40% против
    /// Хога» ничего не говорит, а «25% против Хога» - говорит.
    /// </summary>
    /// <param name="minGames">Меньше встреч - это случайность, а не слабое место.</param>
    public static List<Tough> ToughCards(IReadOnlyList<PlayerBattle> battles, int minGames = 4, int take = 3)
    {
        var decided = battles.Where(b => b.Result != 0).ToList();
        if (decided.Count == 0) return [];
        var baseline = (double)decided.Count(b => b.Result > 0) / decided.Count;

        var byCard = new Dictionary<int, (int Games, int Wins)>();
        foreach (var b in decided)
            foreach (var k in MetaCard.ParseDeckKey(b.OppDeckKey).Distinct())
            {
                byCard.TryGetValue(k, out var v);
                byCard[k] = (v.Games + 1, v.Wins + (b.Result > 0 ? 1 : 0));
            }

        return byCard
            .Where(kv => kv.Value.Games >= minGames)
            .Select(kv => new Tough(kv.Key, kv.Value.Games, kv.Value.Wins,
                (double)kv.Value.Wins / kv.Value.Games - baseline))
            .Where(t => t.Delta < 0)
            .OrderBy(t => t.Delta)
            .ThenByDescending(t => t.Games)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Самая похожая колода меты: сначала по числу общих карт, при равенстве - по
    /// числу игр. Карту и её эволюцию считаем одной картой: для «та же колода или
    /// нет» эволюция не важна, а без этого колода с Эво-Рыцарем не нашла бы себя.
    /// </summary>
    public static (string Key, int Shared, int Games, int Wins)? ClosestMetaDeck(
        IReadOnlyCollection<int> deck,
        IEnumerable<(string Key, int Games, int Wins, int Draws)> meta,
        int minShared = 6,
        int minGames = 15)
    {
        var mine = deck.Select(k => Math.Abs(k)).ToHashSet();
        (string Key, int Shared, int Games, int Wins)? best = null;

        foreach (var m in meta)
        {
            if (m.Games < minGames) continue;
            var shared = MetaCard.ParseDeckKey(m.Key).Select(k => Math.Abs(k)).Distinct().Count(mine.Contains);
            if (shared < minShared) continue;
            if (best is null || shared > best.Value.Shared
                || (shared == best.Value.Shared && m.Games > best.Value.Games))
                best = (m.Key, shared, m.Games, m.Wins);
        }
        return best;
    }
}
