namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Определение результата матча по боевому логу — чистая логика, без API и без базы.
///
/// Пара опознаётся по полному совпадению тегов с обеих сторон: в 2х2 это четыре тега,
/// в 1х1 — два. Случайно такой бой не возникает: чтобы совпали и оба напарника, и оба
/// соперника, эти четверо должны были сесть играть именно друг против друга. Поэтому
/// фильтровать по режиму боя не нужно — хоть дружеский, хоть в клане, хоть в турнире.
///
/// Всё, что не сошлось однозначно, остаётся организатору: лучше не засчитать, чем
/// засчитать не то. Это и есть разница между «подсказкой» и «выдумкой».
/// </summary>
public static class TournamentAutoResult
{
    /// <summary>Бой, сведённый к тому, что нужно для опознания пары.</summary>
    /// <param name="TeamTags">Теги стороны, чей лог читаем.</param>
    public record BattleFact(
        DateTime TimeUtc,
        IReadOnlyCollection<string> TeamTags,
        IReadOnlyCollection<string> OpponentTags,
        int CrownsFor,
        int CrownsAgainst);

    /// <param name="Decided">Кто-то набрал нужное число побед — результат можно ставить.</param>
    /// <param name="Counted">Сколько боёв пары учтено; 0 при Decided=false значит «ещё не играли».</param>
    public record Outcome(int ScoreA, int ScoreB, bool Decided, int Counted);

    /// <summary>
    /// Считает счёт пары по боям из лога.
    /// </summary>
    /// <param name="battles">Лог стороны A: CrownsFor — её короны.</param>
    /// <param name="notBefore">
    /// Раньше этого момента бои не считаем. Иначе вчерашняя встреча тех же команд
    /// в ладдере закроет сегодняшний матч, а при исправлении сетки — ещё и бой,
    /// сыгранный до того, как пара вообще сошлась в этом раунде.
    /// </param>
    public static Outcome Resolve(
        IEnumerable<BattleFact> battles,
        IReadOnlyCollection<string> tagsA,
        IReadOnlyCollection<string> tagsB,
        int winsNeeded,
        DateTime notBefore)
    {
        var setA = new HashSet<string>(tagsA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(tagsB, StringComparer.OrdinalIgnoreCase);

        // Пустая или неполная заявка — опознавать нечем, молча отдаём организатору.
        if (setA.Count == 0 || setB.Count == 0 || setA.Overlaps(setB))
            return new Outcome(0, 0, false, 0);

        var relevant = battles
            .Where(b => b.TimeUtc >= notBefore)
            .Where(b => Same(b.TeamTags, setA) && Same(b.OpponentTags, setB))
            .OrderBy(b => b.TimeUtc)
            .ToList();

        var scoreA = 0;
        var scoreB = 0;
        var counted = 0;

        foreach (var b in relevant)
        {
            // Ничья по коронам победы никому не даёт — просто пропускаем бой.
            if (b.CrownsFor == b.CrownsAgainst) continue;

            if (b.CrownsFor > b.CrownsAgainst) scoreA++; else scoreB++;
            counted++;

            // Дальше не считаем: всё, что они сыграли после решающего боя, — это уже
            // не матч, а реванш ради интереса, и он не должен менять счёт.
            if (scoreA >= winsNeeded || scoreB >= winsNeeded) break;
        }

        return new Outcome(scoreA, scoreB, scoreA >= winsNeeded || scoreB >= winsNeeded, counted);
    }

    /// <summary>Совпадение состава стороны: те же теги, столько же, без «плюс случайный напарник».</summary>
    private static bool Same(IReadOnlyCollection<string> actual, HashSet<string> expected)
    {
        if (actual.Count != expected.Count) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in actual)
        {
            if (!expected.Contains(tag)) return false;
            if (!seen.Add(tag)) return false;   // один и тот же тег дважды — не наш состав
        }
        return true;
    }
}
