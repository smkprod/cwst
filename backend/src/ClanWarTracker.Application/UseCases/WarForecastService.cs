using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Прогноз славы клана и игроков на конец дня / недели.
///
/// Привязан к реальным правилам River Race:
///  — война идёт 4 дня (чт–пн), PeriodIndex 3..6; дни 0..2 — тренировка, славы не дают;
///  — у игрока 4 колоды в день;
///  — слава за колоду ограничена правилами игры: поражение = 100, победа = 200,
///    в дуэли победа = 250. Любая сыгранная колода даёт от 100 до 250 славы;
///  — в клане максимум 50 человек, т.е. потолок 50 × 4 = 200 атак в день.
///    В списке участников API могут висеть ушедшие из клана (до 100 записей) —
///    атаковать дальше могут только реальные члены клана.
///
/// Алгоритм (эвристика, без ML):
///  avg = clamp(Fame / DecksUsed, 100, 250)        // средняя слава за атаку игрока
///  attendance = min(1, DecksUsed / (4*warDaysPassed))  // как часто играет
///  urgency = f(hoursLeft)                          // падает у дедлайна
///  expectedDecksLeft = (4 - DecksUsedToday) * attendance * urgency
///  playerDayFame = Fame + expectedDecksLeft * avg
///  playerWeekFame = playerDayFame + remainingWarDays * 4 * attendance * avg
/// </summary>
public class WarForecastService
{
    /// <summary>Стандартная River Race: дни 0..2 — тренировка, 3..6 — война (чт–пн).</summary>
    private const int FirstWarPeriodIndex = 3;
    private const int LastWarPeriodIndex = 6;
    private const int TotalWarDays = 4;
    private const int DecksPerDayPerPlayer = 4;

    /// <summary>В клане не может быть больше 50 человек → максимум 200 атак в день.</summary>
    private const int MaxClanMembers = 50;

    /// <summary>Минимальная слава за сыгранную колоду — поражение в обычном бою.</summary>
    private const double MinFamePerDeck = 100;

    /// <summary>Максимальная слава за колоду — победа в дуэли.</summary>
    private const double MaxFamePerDeck = 250;

    /// <summary>База для тех, кто ещё не атаковал: 50% винрейта → (200 + 100) / 2.</summary>
    private const double BaselineFamePerDeck = 150;

    /// <summary>
    /// Уточняет WarDecksUsed участников по снапшоту первого военного дня недели.
    /// CR API в DecksUsed считает и тренировочные бои (они славы не дают), поэтому
    /// «слава/атака» без поправки занижена. Тренировочные колоды игрока =
    /// (DecksUsed − DecksUsedToday) на снапшоте дня 3 — до него шла только тренировка.
    /// </summary>
    public static void RefineWarDecks(WarStatus war, WarSnapshot? firstWarDaySnapshot)
    {
        // Тренировка (0) и первый военный день (DecksUsedToday) уже точны — см. ApiClient
        if (!war.IsWarDay || war.PeriodIndex <= FirstWarPeriodIndex || firstWarDaySnapshot is null)
            return;

        var trainingDecks = firstWarDaySnapshot.Players
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Math.Max(0, g.First().DecksUsed - g.First().DecksUsedToday),
                StringComparer.OrdinalIgnoreCase);

        foreach (var p in war.Participants)
        {
            if (!trainingDecks.TryGetValue(p.PlayerTag, out var training)) continue;
            // Военных колод не меньше, чем сыграно сегодня, и не больше недельного итога
            p.WarDecksUsed = Math.Clamp(p.DecksUsed - training, p.DecksUsedToday, p.DecksUsed);
        }
    }

    public ClanForecastDto BuildClanForecast(WarStatus war, IReadOnlyList<PlayerStatusDto> players, int hoursLeft)
    {
        var projectedDayFame = players.Sum(p => p.ProjectedDayFame);
        var projectedWeekFame = players.Sum(p => p.ProjectedWeekFame);

        var totalFame = war.Participants.Sum(p => p.Fame);
        var totalDecksUsedToday = war.Participants.Sum(p => p.DecksUsedToday);
        var activePlayers = war.Participants.Count(p => p.DecksUsed > 0);

        // Потолок по правилам игры: максимум 50 человек × 4 колоды = 200 атак в день,
        // сколько бы записей ни висело в списке участников API.
        var rosterSize = Math.Min(war.Participants.Count, MaxClanMembers);
        var maxDecksToday = rosterSize * DecksPerDayPerPlayer;
        var expectedRemainingAttacks = war.IsWarDay
            ? Math.Clamp(maxDecksToday - totalDecksUsedToday, 0, maxDecksToday)
            : 0;

        var totalWarDecksUsed = war.Participants.Sum(p => p.WarDecksUsed);
        var clanAvgFamePerAttack = ClampFamePerDeck(
            totalWarDecksUsed > 0 ? (double)totalFame / totalWarDecksUsed : BaselineFamePerDeck);

        // Trend: сравниваем ожидаемый итог сегодняшнего дня со средней славой за уже
        // завершённые военные дни (текущий день не учитываем — он ещё идёт).
        var warDaysPassed = WarDaysPassed(war.PeriodIndex);
        var completedWarDays = war.IsWarDay ? warDaysPassed - 1 : warDaysPassed;

        string trend;
        if (!war.IsWarDay || completedWarDays <= 0)
        {
            trend = "onPace"; // первый военный день — сравнивать ещё не с чем
        }
        else
        {
            // Слава, набитая сегодня ≈ сыгранные сегодня колоды × средняя слава за колоду
            var todayFameSoFar = totalDecksUsedToday * clanAvgFamePerAttack;
            var avgDailyFame = Math.Max(1, (totalFame - todayFameSoFar) / completedWarDays);
            // Полный ожидаемый итог сегодняшнего дня: уже набитое сегодня + остаток прогноза
            var projectedTodayTotal = todayFameSoFar + Math.Max(0, projectedDayFame - totalFame);

            if (projectedTodayTotal > avgDailyFame * 1.10)
                trend = "ahead";
            else if (projectedTodayTotal < avgDailyFame * 0.90)
                trend = "behind";
            else
                trend = "onPace";
        }

        // Confidence:
        //   — растёт по мере того, как война идёт (больше данных)
        //   — растёт с количеством активных игроков
        var dataDepth = Math.Clamp(warDaysPassed / (double)TotalWarDays, 0, 1);     // 0..1
        var participation = rosterSize > 0
            ? Math.Min(1.0, activePlayers / (double)rosterSize)
            : 0;
        var confidence = (int)Math.Round((0.4 + 0.4 * dataDepth + 0.2 * participation) * 100);
        confidence = Math.Clamp(confidence, 30, 95);

        return new ClanForecastDto(
            ProjectedDayFame: projectedDayFame,
            ProjectedWeekFame: projectedWeekFame,
            ExpectedRemainingAttacksToday: expectedRemainingAttacks,
            Confidence: confidence,
            Trend: trend);
    }

    /// <param name="canStillAttack">
    /// false — игрок числится в списке участников, но уже не в клане (свыше 50 мест):
    /// будущих атак у него не будет, прогноз = текущая слава.
    /// </param>
    public PlayerProjection ProjectPlayer(
        WarParticipant p, WarStatus war, int hoursLeft, double clanAvgFamePerAttack, bool canStillAttack = true)
    {
        var warDaysPassed = WarDaysPassed(war.PeriodIndex);
        var remainingWarDays = RemainingWarDays(war.PeriodIndex);

        // 1) средняя слава за ВОЕННУЮ атаку (тренировочные колоды не считаем —
        //    они входят в DecksUsed, но славы не дают). Границы по правилам: 100..250.
        var avgFame = p.WarDecksUsed > 0
            ? ClampFamePerDeck((double)p.Fame / p.WarDecksUsed)
            : ClampFamePerDeck(clanAvgFamePerAttack > 0 ? clanAvgFamePerAttack : BaselineFamePerDeck);

        if (!canStillAttack)
            return new PlayerProjection(avgFame, p.Fame, p.Fame);

        // 2) посещаемость: как полно игрок использует свои 4 военные колоды в среднем за день
        var attendance = warDaysPassed > 0
            ? Math.Clamp(p.WarDecksUsed / (double)(warDaysPassed * DecksPerDayPerPlayer), 0, 1)
            : (war.IsWarDay ? 0.85 : 0.7); // на старте — оптимистичная база

        // 3) сегодня атаки возможны только в военный день (тренировка славы не даёт)
        double expectedDecksToday = 0;
        if (war.IsWarDay)
        {
            var urgency = TimeUrgencyFactor(hoursLeft, p.DecksUsedToday);
            var remainingDecksToday = Math.Max(0, DecksPerDayPerPlayer - p.DecksUsedToday);
            expectedDecksToday = remainingDecksToday * attendance * urgency;
        }

        var projectedDayFame = (int)Math.Round(p.Fame + expectedDecksToday * avgFame);

        // На неделю: добавляем ожидаемые атаки в оставшиеся (будущие) дни войны
        var expectedDecksFutureDays = remainingWarDays * DecksPerDayPerPlayer * attendance;
        var projectedWeekFame = (int)Math.Round(projectedDayFame + expectedDecksFutureDays * avgFame);

        // Жёсткий потолок по правилам: оставшиеся колоды × 250 славы максимум
        var maxDayFame = p.Fame + (int)((war.IsWarDay ? DecksPerDayPerPlayer - p.DecksUsedToday : 0) * MaxFamePerDeck);
        projectedDayFame = Math.Min(projectedDayFame, maxDayFame);
        var maxWeekFame = maxDayFame + (int)(remainingWarDays * DecksPerDayPerPlayer * MaxFamePerDeck);
        projectedWeekFame = Math.Min(projectedWeekFame, maxWeekFame);

        return new PlayerProjection(avgFame, projectedDayFame, projectedWeekFame);
    }

    private static double ClampFamePerDeck(double fame) =>
        Math.Clamp(fame, MinFamePerDeck, MaxFamePerDeck);

    /// <summary>
    /// 1.0 при большом запасе времени; падает к дедлайну, особенно для тех, кто не начал играть.
    /// </summary>
    private static double TimeUrgencyFactor(int hoursLeft, int decksUsedToday)
    {
        if (hoursLeft >= 10) return 0.95;
        if (hoursLeft >= 6) return 0.85;
        if (hoursLeft >= 3) return decksUsedToday > 0 ? 0.75 : 0.55;
        if (hoursLeft >= 1) return decksUsedToday > 0 ? 0.55 : 0.25;
        return decksUsedToday > 0 ? 0.30 : 0.05; // деадлайн — те, кто не начал, скорее всего сольют
    }

    /// <summary>
    /// Сколько военных дней уже прошло (включая текущий, если он военный).
    /// PeriodIndex: 0..2 — тренировка, 3..6 — война.
    /// </summary>
    private static int WarDaysPassed(int periodIndex)
    {
        if (periodIndex < FirstWarPeriodIndex) return 0;
        return Math.Min(TotalWarDays, periodIndex - FirstWarPeriodIndex + 1);
    }

    /// <summary>Сколько полных военных дней ещё впереди (текущий не считаем).</summary>
    private static int RemainingWarDays(int periodIndex)
    {
        if (periodIndex < FirstWarPeriodIndex) return TotalWarDays;      // тренировка — вся война впереди
        return Math.Clamp(LastWarPeriodIndex - periodIndex, 0, TotalWarDays);
    }

    public record PlayerProjection(double AvgFamePerAttack, int ProjectedDayFame, int ProjectedWeekFame);
}
