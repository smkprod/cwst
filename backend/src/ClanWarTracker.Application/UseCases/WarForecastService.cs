using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Прогноз славы клана и игроков на конец дня / недели.
///
/// Правила River Race:
///  — война идёт 4 дня (чт–пн), PeriodIndex 3..6; дни 0..2 — тренировка, славы не дают;
///  — у игрока 4 колоды в день;
///  — слава за колоду ограничена правилами игры: поражение = 100, победа = 200,
///    в дуэли победа = 250. Любая сыгранная колода даёт от 100 до 250 славы;
///  — в клане максимум 50 человек, т.е. потолок 50 × 4 = 200 атак в день.
///
/// При наличии истории (riverracelog + снапшоты) использует EWMA по последним
/// 10 войнам для уточнения средней славы и посещаемости.
/// </summary>
public class WarForecastService
{
    private const int FirstWarPeriodIndex = 3;
    private const int LastWarPeriodIndex = 6;
    private const int TotalWarDays = 4;
    private const int DecksPerDayPerPlayer = 4;

    private const double MinFamePerDeck = 100;
    private const double MaxFamePerDeck = 250;

    /// <summary>Базовое среднее для cold start (50% винрейта: (200+100)/2).</summary>
    private const double BaselineFamePerDeck = 150;

    /// <summary>
    /// Коэффициент затухания EWMA по войнам: свежая война весит в 0.8^1 = 80%
    /// от самой свежей. Значение 0.8 даёт «полупериод» ≈ 3 войны.
    /// </summary>
    private const double EwmaDecay = 0.8;

    /// <summary>Максимум войн для истории прогноза.</summary>
    private const int HistoryWeeksWindow = 10;

    /// <summary>Минимум войн для использования EWMA (меньше — cold start blend).</summary>
    private const int ColdStartThreshold = 3;

    /// <summary>
    /// Крутизна логистической urgency-кривой.
    /// k=0.4, h0=5 → urgency≈0.95 при 10ч, 0.73 при 5ч, 0.5 при 0ч до дедлайна.
    /// </summary>
    private const double UrgencyK = 0.4;
    private const double UrgencyH0 = 5.0;

    /// <summary>Типичное СКО одной колоды — по биномиальной модели {100,200,250}.</summary>
    private const double SdPerDeck = 55.0;

    /// <summary>
    /// Исторический профиль игрока: EWMA средней славы и посещаемости за последние войны.
    /// Передаётся в ProjectPlayer для точного прогноза.
    /// </summary>
    public record PlayerHistoryProfile(
        /// <summary>EWMA avg(fame/warDeck), 100..250. NaN = нет данных (cold start).</summary>
        double EwmaFamePerDeck,
        /// <summary>EWMA доли использованных колод за неделю (0..1). NaN = нет данных.</summary>
        double EwmaAttendance,
        /// <summary>Количество недель в профиле (0 = полный cold start).</summary>
        int WeeksCount);

    /// <summary>
    /// Строит EWMA-профили для всех игроков по данным прошлых войн.
    /// Принимает map: (сезон, неделя) → (playerTag → (Fame, DecksUsed)) — уже отсортированный
    /// по убыванию новизны. Читается одним батчем в вызывающем коде.
    /// </summary>
    public static Dictionary<string, PlayerHistoryProfile> BuildHistoryProfiles(
        IReadOnlyList<IReadOnlyDictionary<string, (int Fame, int Decks)>> weeks,
        IEnumerable<string> playerTags)
    {
        var result = new Dictionary<string, PlayerHistoryProfile>(StringComparer.OrdinalIgnoreCase);
        var window = weeks.Take(HistoryWeeksWindow).ToList();

        foreach (var tag in playerTags)
        {
            double sumFame = 0, sumAtt = 0, sumW = 0;
            int count = 0;

            for (int t = 0; t < window.Count; t++)
            {
                var wt = Math.Pow(EwmaDecay, t);
                window[t].TryGetValue(tag, out var pd);

                // Посещаемость = использованных колод / максимальных за 4 военных дня
                var att = Math.Clamp((double)pd.Decks / (TotalWarDays * DecksPerDayPerPlayer), 0, 1);
                sumAtt += wt * att;

                if (pd.Fame > 0 && pd.Decks > 0)
                {
                    var fpd = Math.Clamp((double)pd.Fame / pd.Decks, MinFamePerDeck, MaxFamePerDeck);
                    sumFame += wt * fpd;
                }
                else
                {
                    // Не участвовал — вклад нулевой, но вес идёт в знаменатель
                    sumFame += wt * 0;
                }

                sumW += wt;
                if (pd.Fame > 0 || pd.Decks > 0) count++;
            }

            if (sumW < 1e-9 || window.Count == 0)
            {
                result[tag] = new PlayerHistoryProfile(double.NaN, double.NaN, 0);
                continue;
            }

            var ewmaAtt = sumAtt / sumW;

            // Если игрок в большинстве недель не участвовал — fame per deck сырой
            // Вместо делить на suмW (который включает «0» от непосещённых недель)
            // считаем только по неделям с реальными боями
            double ewmaFpd;
            if (count == 0)
            {
                ewmaFpd = double.NaN;
            }
            else
            {
                double sumFameActual = 0, sumWActual = 0;
                for (int t = 0; t < window.Count; t++)
                {
                    var wt = Math.Pow(EwmaDecay, t);
                    window[t].TryGetValue(tag, out var pd);
                    if (pd.Fame > 0 && pd.Decks > 0)
                    {
                        var fpd = Math.Clamp((double)pd.Fame / pd.Decks, MinFamePerDeck, MaxFamePerDeck);
                        sumFameActual += wt * fpd;
                        sumWActual += wt;
                    }
                }
                ewmaFpd = sumWActual > 1e-9
                    ? Math.Clamp(sumFameActual / sumWActual, MinFamePerDeck, MaxFamePerDeck)
                    : double.NaN;
            }

            result[tag] = new PlayerHistoryProfile(ewmaFpd, ewmaAtt, window.Count);
        }

        return result;
    }

    /// <summary>
    /// Уточняет WarDecksUsed участников по снапшоту первого военного дня недели.
    /// CR API в DecksUsed считает и тренировочные бои (они славы не дают), поэтому
    /// «слава/атака» без поправки занижена.
    /// </summary>
    public static void RefineWarDecks(WarStatus war, WarSnapshot? firstWarDaySnapshot)
    {
        if (!war.IsWarDay || war.PeriodIndex <= FirstWarPeriodIndex || firstWarDaySnapshot is null)
            return;

        var trainingDecks = firstWarDaySnapshot.Players
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Math.Max(0, g.First().DecksUsed - g.First().DecksUsedToday),
                StringComparer.OrdinalIgnoreCase);

        foreach (var p in war.Participants)
        {
            if (!trainingDecks.TryGetValue(p.PlayerTag, out var training)) continue;
            p.WarDecksUsed = Math.Clamp(p.DecksUsed - training, p.DecksUsedToday, p.DecksUsed);
        }
    }

    public ClanForecastDto BuildClanForecast(
        WarStatus war,
        IReadOnlyList<PlayerStatusDto> players,
        int hoursLeft)
    {
        var totalFame = players.Sum(p => p.Fame);
        var totalDecksUsedToday = players.Sum(p => p.DecksUsedToday);
        var activePlayers = players.Count(p => p.DecksUsed > 0);
        var rosterSize = players.Count;
        var maxDecksToday = rosterSize * DecksPerDayPerPlayer;

        var totalWarDecksUsed = players.Sum(p => p.WarDecksUsed);
        var clanAvgFamePerAttack = ClampFamePerDeck(
            totalWarDecksUsed > 0 ? (double)totalFame / totalWarDecksUsed : BaselineFamePerDeck);

        // Очки ТОЛЬКО текущего военного дня (двигают лодку). Накопленная за неделю
        // слава тут не нужна — нужен итог именно сегодняшнего дня.
        var ourClan = war.RaceClans.FirstOrDefault(c =>
            string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
        var todayPointsSoFar = ourClan?.PeriodPoints ?? 0;

        // Прогноз в стиле RoyaleAPI: считаем, что оставшиеся колоды будут сыграны
        // по средней силе клана. Активный клан доигрывает почти всё — занижаем
        // участие только у самого дедлайна (мало часов до конца дня).
        var remainingDecksToday = war.IsWarDay
            ? Math.Max(0, maxDecksToday - totalDecksUsedToday)
            : 0;
        var dayParticipation = Math.Clamp(hoursLeft / 3.0, 0.6, 1.0);
        var expectedRemainingAttacks = (int)Math.Round(remainingDecksToday * dayParticipation);

        // Итог СЕГОДНЯШНЕГО дня к его концу = набрано сегодня + оставшиеся колоды × avg
        var projectedDayFame = todayPointsSoFar
            + (int)Math.Round(remainingDecksToday * dayParticipation * clanAvgFamePerAttack);

        // Прогноз накопленной славы к концу недели (все 4 военных дня).
        var remainingWarDays = RemainingWarDays(war.PeriodIndex);
        var futureDecks = remainingWarDays * maxDecksToday;
        var projectedWeekFame = totalFame
            + (int)Math.Round(remainingDecksToday * dayParticipation * clanAvgFamePerAttack)
            + (int)Math.Round(futureDecks * 0.9 * clanAvgFamePerAttack);

        // Trend: ожидаемый итог текущего дня vs средняя по завершённым дням
        var warDaysPassed = WarDaysPassed(war.PeriodIndex);
        var completedWarDays = war.IsWarDay ? warDaysPassed - 1 : warDaysPassed;

        string trend;
        if (!war.IsWarDay || completedWarDays <= 0)
        {
            trend = "onPace";
        }
        else
        {
            // Средние очки за завершённый день vs прогноз сегодняшнего дня
            var avgDailyFame = Math.Max(1, (double)(totalFame - todayPointsSoFar) / completedWarDays);

            if (projectedDayFame > avgDailyFame * 1.10)
                trend = "ahead";
            else if (projectedDayFame < avgDailyFame * 0.90)
                trend = "behind";
            else
                trend = "onPace";
        }

        // Confidence: растёт с глубиной данных войны и активностью клана
        var dataDepth = Math.Clamp(warDaysPassed / (double)TotalWarDays, 0, 1);
        var participation = rosterSize > 0
            ? Math.Min(1.0, activePlayers / (double)rosterSize)
            : 0;
        var confidence = (int)Math.Round((0.4 + 0.4 * dataDepth + 0.2 * participation) * 100);
        confidence = Math.Clamp(confidence, 30, 95);

        // Доверительный интервал (±1σ) для очков сегодняшнего дня:
        // предполагаем нормальное распределение суммы N атак, каждая sd ≈ 55 медалей
        var sdTotal = Math.Sqrt(expectedRemainingAttacks) * SdPerDeck;
        var maxDayPoints = todayPointsSoFar + remainingDecksToday * (int)MaxFamePerDeck;
        var low = Math.Clamp(projectedDayFame - (int)sdTotal, todayPointsSoFar, projectedDayFame);
        var high = Math.Clamp(projectedDayFame + (int)sdTotal, projectedDayFame, maxDayPoints);

        return new ClanForecastDto(
            ProjectedDayFame: projectedDayFame,
            ProjectedWeekFame: projectedWeekFame,
            ExpectedRemainingAttacksToday: expectedRemainingAttacks,
            Confidence: confidence,
            Trend: trend,
            ProjectedDayFameLow: war.IsWarDay ? low : projectedDayFame,
            ProjectedDayFameHigh: war.IsWarDay ? high : projectedDayFame);
    }

    /// <param name="canStillAttack">
    /// false — игрок уже не в клане; прогноз = текущая слава.
    /// </param>
    /// <param name="history">
    /// EWMA-профиль из последних 10 войн. null — cold start / Free тариф.
    /// </param>
    public PlayerProjection ProjectPlayer(
        WarParticipant p, WarStatus war, int hoursLeft, double clanAvgFamePerAttack,
        bool canStillAttack = true,
        PlayerHistoryProfile? history = null)
    {
        var warDaysPassed = WarDaysPassed(war.PeriodIndex);
        var remainingWarDays = RemainingWarDays(war.PeriodIndex);

        // 1) Средняя слава за ВОЕННУЮ атаку
        double avgFame;
        if (p.WarDecksUsed > 0)
        {
            // Наблюдаемое на текущей неделе — самый надёжный сигнал
            avgFame = ClampFamePerDeck((double)p.Fame / p.WarDecksUsed);
        }
        else if (history is not null && !double.IsNaN(history.EwmaFamePerDeck))
        {
            // Ещё не атаковал на этой неделе — берём EWMA истории
            avgFame = history.EwmaFamePerDeck;
        }
        else
        {
            // Cold start: клановый средний или базовый 150
            avgFame = ClampFamePerDeck(clanAvgFamePerAttack > 0 ? clanAvgFamePerAttack : BaselineFamePerDeck);
        }

        if (!canStillAttack)
            return new PlayerProjection(avgFame, p.Fame, p.Fame);

        // 2) Посещаемость: доля использованных военных колод в среднем за день
        double attendance;
        if (history is not null && history.WeeksCount >= ColdStartThreshold)
        {
            // Есть достаточно истории — смешиваем EWMA с фактом текущей недели
            var histBase = double.IsNaN(history.EwmaAttendance) ? 0.75 : history.EwmaAttendance;
            var blendFactor = Math.Clamp(warDaysPassed / (double)TotalWarDays, 0, 1);
            var currentWeekAtt = warDaysPassed > 0
                ? Math.Clamp(p.WarDecksUsed / (double)(warDaysPassed * DecksPerDayPerPlayer), 0, 1)
                : histBase;
            attendance = (1 - blendFactor) * histBase + blendFactor * currentWeekAtt;
        }
        else if (warDaysPassed > 0)
        {
            // Мало истории — считаем только по текущей неделе
            attendance = Math.Clamp(p.WarDecksUsed / (double)(warDaysPassed * DecksPerDayPerPlayer), 0, 1);
        }
        else
        {
            // Начало войны — оптимистичная база
            var histBase = history is not null && !double.IsNaN(history.EwmaAttendance)
                ? history.EwmaAttendance
                : (war.IsWarDay ? 0.85 : 0.7);
            attendance = histBase;
        }

        // 3) Urgency: гладкая логистика вместо ступенчатой таблицы
        double expectedDecksToday = 0;
        if (war.IsWarDay)
        {
            var urgency = LogisticUrgency(hoursLeft, p.DecksUsedToday);
            var remainingDecksToday = Math.Max(0, DecksPerDayPerPlayer - p.DecksUsedToday);
            expectedDecksToday = remainingDecksToday * attendance * urgency;
        }

        var projectedDayFame = (int)Math.Round(p.Fame + expectedDecksToday * avgFame);

        // 4) Прогноз на неделю: оставшиеся военные дни × посещаемость × avg
        var expectedDecksFutureDays = remainingWarDays * DecksPerDayPerPlayer * attendance;
        var projectedWeekFame = (int)Math.Round(projectedDayFame + expectedDecksFutureDays * avgFame);

        // 5) Жёсткие потолки по правилам игры
        var maxDayFame = p.Fame + (war.IsWarDay ? (int)((DecksPerDayPerPlayer - p.DecksUsedToday) * MaxFamePerDeck) : 0);
        projectedDayFame = Math.Min(projectedDayFame, maxDayFame);
        var maxWeekFame = maxDayFame + (int)(remainingWarDays * DecksPerDayPerPlayer * MaxFamePerDeck);
        projectedWeekFame = Math.Min(projectedWeekFame, maxWeekFame);

        return new PlayerProjection(avgFame, projectedDayFame, projectedWeekFame);
    }

    private static double ClampFamePerDeck(double fame) =>
        Math.Clamp(fame, MinFamePerDeck, MaxFamePerDeck);

    /// <summary>
    /// Гладкая urgency-кривая через логистическую функцию.
    /// При hoursLeft ≥ 10 ≈ 0.95; при 5 ч ≈ 0.73; при 0 ≈ 0.5.
    /// Дополнительный штраф × 0.4 для тех, кто ещё не начал и осталось < 3 ч.
    /// </summary>
    private static double LogisticUrgency(int hoursLeft, int decksUsedToday)
    {
        var u = 1.0 / (1.0 + Math.Exp(-UrgencyK * (hoursLeft - UrgencyH0)));
        if (hoursLeft < 3 && decksUsedToday == 0)
            u *= 0.4; // те, кто не начал у дедлайна — скорее всего не сыграют
        return Math.Clamp(u, 0.05, 0.97);
    }

    private static int WarDaysPassed(int periodIndex)
    {
        if (periodIndex < FirstWarPeriodIndex) return 0;
        return Math.Min(TotalWarDays, periodIndex - FirstWarPeriodIndex + 1);
    }

    private static int RemainingWarDays(int periodIndex)
    {
        if (periodIndex < FirstWarPeriodIndex) return TotalWarDays;
        return Math.Clamp(LastWarPeriodIndex - periodIndex, 0, TotalWarDays);
    }

    public record PlayerProjection(double AvgFamePerAttack, int ProjectedDayFame, int ProjectedWeekFame);
}
