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

    /// <summary>
    /// Бои, по которым считается тильт: только ладдер и Путь легенд.
    ///
    /// Война клана сюда не идёт: там четыре разные колоды и совсем другая ставка,
    /// а сигнал «пауза» посреди военного дня только мешает доиграть колоды. Турниры
    /// и испытания - тоже нет: кубки в них не теряются.
    /// </summary>
    public static bool IsTiltEligible(string? type) =>
        type is not null
        && !type.Contains("riverRace", StringComparison.OrdinalIgnoreCase)
        && (type.Equals("PvP", StringComparison.OrdinalIgnoreCase)
            || type.Contains("legend", StringComparison.OrdinalIgnoreCase)
            || type.Contains("ranked", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Каждый бой вместе с тем, что было перед ним в этом заходе: сколько поражений
    /// подряд (больше нуля) или что перед ним победа (меньше нуля). Новый заход
    /// начинает счёт с нуля; ничья серию не рвёт и не продолжает.
    /// </summary>
    private static IEnumerable<(PlayerBattle Battle, int StreakBefore, int Session)> WithStreak(IReadOnlyList<PlayerBattle> battles)
    {
        int streak = 0, session = 0;
        DateTime? last = null;
        foreach (var b in battles)
        {
            if (last is { } prev && b.BattleTimeUtc - prev > SessionGap)
            {
                streak = 0;
                session++;
            }
            yield return (b, streak, session);

            if (b.Result < 0) streak = streak > 0 ? streak + 1 : 1;
            else if (b.Result > 0) streak = -1;
            last = b.BattleTimeUtc;
        }
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

    /// <param name="Alert">Писать сейчас.</param>
    /// <param name="LastBattleUtc">Последний бой захода - запоминается, чтобы не писать про ту же серию снова.</param>
    /// <param name="LossStreak">Поражений подряд в конце захода.</param>
    public record TiltCheck(bool Alert, DateTime? LastBattleUtc, int LossStreak);

    /// <summary>
    /// Пора ли написать «Стоп-тильт»: игрок прямо сейчас в заходе и слил два боя
    /// подряд, а в этом заходе мы ещё не писали.
    ///
    /// «Прямо сейчас» - последний бой не старше <paramref name="freshness"/>: писать
    /// про серию, после которой человек уже час как не играет, бессмысленно и
    /// раздражает. Один заход - одно сообщение: вторая серия в том же заходе значит,
    /// что первый совет не услышали, и третий не поможет.
    /// </summary>
    public static TiltCheck ShouldAlertTilt(
        IReadOnlyList<PlayerBattle> battles, DateTime nowUtc, DateTime? lastAlertBattleUtc, TimeSpan freshness,
        int threshold = 2)
    {
        if (battles.Count == 0) return new TiltCheck(false, null, 0);

        var last = battles[^1];
        if (nowUtc - last.BattleTimeUtc > freshness) return new TiltCheck(false, last.BattleTimeUtc, 0);

        // Начало захода: идём назад, пока бои идут без больших перерывов.
        var start = battles.Count - 1;
        while (start > 0 && battles[start].BattleTimeUtc - battles[start - 1].BattleTimeUtc <= SessionGap)
            start--;
        var sessionStart = battles[start].BattleTimeUtc;

        // Поражения подряд в конце захода; ничьи пропускаем, победа обрывает счёт.
        var streak = 0;
        for (var i = battles.Count - 1; i >= start; i--)
        {
            if (battles[i].Result > 0) break;
            if (battles[i].Result < 0) streak++;
        }

        var alreadyThisSession = lastAlertBattleUtc is { } la && la >= sessionStart;
        return new TiltCheck(streak >= Math.Max(2, threshold) && !alreadyThisSession, last.BattleTimeUtc, streak);
    }

    /// <summary>Сколько наблюдений «после двух поражений» нужно, чтобы назвать личный процент.</summary>
    public const int ProfileMinSamples = 8;

    /// <summary>Сколько боёв нужно для тильт-типа: меньше - это ещё не характер, а один вечер.</summary>
    public const int ProfileMinBattles = 20;

    /// <param name="BasePercent">Обычный процент побед.</param>
    /// <param name="After2Percent">
    /// Процент после двух поражений подряд, сглаженный к обычному: (победы + 8·обычный) / (n + 8).
    /// На восьми боях сырая цифра скачет на ±30 п.п., и одна нелепая цифра, показанная
    /// в клане, убила бы доверие ко всему остальному. null - наблюдений ещё мало.
    /// </param>
    /// <param name="Type">ice / boiling / volcano; null - рано судить.</param>
    public record TiltProfile(int Games, double BasePercent, int After2Games, double? After2Percent, string? Type);

    /// <summary>Тильт-тип: 🧊 Лёд (разница до 3 п.п.), 🌡 Закипаешь (до 12), 🌋 Вулкан (больше).</summary>
    public static TiltProfile Profile(IReadOnlyList<PlayerBattle> battles)
    {
        var totals = Count(battles);
        if (totals.Games == 0) return new TiltProfile(0, 0, 0, null, null);

        var tilt = Tilt(battles);
        var baseRate = (double)totals.Wins / totals.Games;
        if (tilt.AfterTwoLosses < ProfileMinSamples || totals.Games < ProfileMinBattles)
            return new TiltProfile(totals.Games, Math.Round(baseRate * 100, 1), tilt.AfterTwoLosses, null, null);

        var after2 = (tilt.AfterTwoLossesWins + ProfileMinSamples * baseRate) / (tilt.AfterTwoLosses + ProfileMinSamples);
        var diff = (baseRate - after2) * 100;
        var type = diff <= 3 ? "ice" : diff <= 12 ? "boiling" : "volcano";
        return new TiltProfile(totals.Games, Math.Round(baseRate * 100, 1), tilt.AfterTwoLosses,
            Math.Round(after2 * 100, 1), type);
    }

    /// <summary>Бои, сыгранные «в тильте»: после <paramref name="threshold"/> и более поражений подряд в заходе.</summary>
    public static List<PlayerBattle> InTilt(IReadOnlyList<PlayerBattle> battles, int threshold = 2) =>
        WithStreak(battles).Where(x => x.StreakBefore >= threshold).Select(x => x.Battle).ToList();

    /// <param name="AtUtc">Бой, после которого бот написал бы.</param>
    /// <param name="AfterWins">Что было дальше в том же заходе.</param>
    public record Moment(DateTime AtUtc, int AfterWins, int AfterLosses);

    /// <summary>
    /// Моменты, когда «Стоп-тильт» написал бы: серия дошла до порога, по одному на
    /// заход, - и чем кончилось продолжение. Для бесплатных это вместо замка:
    /// «за 14 дней было 4 момента, после них 3–9».
    /// </summary>
    public static List<Moment> Moments(IReadOnlyList<PlayerBattle> battles, int threshold, DateTime sinceUtc)
    {
        var result = new List<(DateTime At, int W, int L)>();
        var momentSession = -1;
        int? open = null;

        foreach (var (b, before, session) in WithStreak(battles))
        {
            if (open is int oi && momentSession == session)
            {
                var m = result[oi];
                if (b.Result > 0) m.W++;
                else if (b.Result < 0) m.L++;
                result[oi] = m;
            }

            var after = b.Result < 0 ? (before > 0 ? before + 1 : 1) : before;
            if (b.Result < 0 && after == Math.Max(2, threshold) && momentSession != session && b.BattleTimeUtc >= sinceUtc)
            {
                result.Add((b.BattleTimeUtc, 0, 0));
                open = result.Count - 1;
                momentSession = session;
            }
        }

        return result.Select(m => new Moment(m.At, m.W, m.L)).ToList();
    }

    /// <summary>
    /// Своя лучшая колода за период - для «🟢 Можно. Начни с колоды X». Процент
    /// сглажен к обычному, чтобы колода «3 из 3» не обгоняла проверенную.
    /// </summary>
    public static (string Key, int Games, int Wins, double Percent)? BestDeck(
        IReadOnlyList<PlayerBattle> battles, int minGames = 5)
    {
        var totals = Count(battles);
        if (totals.Games == 0) return null;
        var baseRate = (double)totals.Wins / totals.Games;

        var best = battles
            .GroupBy(b => b.DeckKey)
            .Select(g => (Key: g.Key, Games: g.Count(), Wins: g.Count(b => b.Result > 0)))
            .Where(d => d.Games >= minGames)
            .Select(d => (d.Key, d.Games, d.Wins, Smoothed: (d.Wins + minGames * baseRate) / (d.Games + minGames)))
            .OrderByDescending(d => d.Smoothed)
            .FirstOrDefault();
        if (best.Key is null) return null;
        return (best.Key, best.Games, best.Wins, Math.Round(100.0 * best.Wins / best.Games, 1));
    }

    /// <summary>
    /// Заход, в который входит бой <paramref name="anchorUtc"/>: соседние бои без перерыва
    /// больше <see cref="SessionGap"/>. Пусто - такого боя в списке нет.
    /// </summary>
    public static List<PlayerBattle> SessionAround(IReadOnlyList<PlayerBattle> battles, DateTime anchorUtc)
    {
        var i = -1;
        for (var k = 0; k < battles.Count; k++)
            if (battles[k].BattleTimeUtc == anchorUtc) { i = k; break; }
        if (i < 0) return [];

        int from = i, to = i;
        while (from > 0 && battles[from].BattleTimeUtc - battles[from - 1].BattleTimeUtc <= SessionGap) from--;
        while (to < battles.Count - 1 && battles[to + 1].BattleTimeUtc - battles[to].BattleTimeUtc <= SessionGap) to++;
        return battles.Skip(from).Take(to - from + 1).ToList();
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
    /// <param name="minGames">
    /// Меньше встреч - это случайность, а не слабое место. Было 4, но на четырёх боях
    /// одно невезение превращало любую карту в «контру», и совет выходил шумом.
    /// </param>
    public static List<Tough> ToughCards(IReadOnlyList<PlayerBattle> battles, int minGames = 5, int take = 3)
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
