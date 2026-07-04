using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

/// <summary>Снимок текущей River Race клана, собранный из Clash Royale API.</summary>
public class WarStatus
{
    public required string ClanTag { get; set; }
    public required string PeriodType { get; set; }      // "training" | "warDay" | "colosseum"
    public int PeriodIndex { get; set; }
    public int SeasonId { get; set; }                    // сезон CR
    public int SectionIndex { get; set; }                // неделя внутри сезона
    public DateTime DayEndsAtUtc { get; set; }           // конец текущего дня войны
    public List<WarParticipant> Participants { get; set; } = [];

    /// <summary>Все кланы текущей гонки (включая наш) — для таблицы «ситуация в гонке».</summary>
    public List<RaceClanStanding> RaceClans { get; set; } = [];

    /// <summary>
    /// Официальный по-дневный лог гонки (periodLogs из /currentriverrace) для НАШЕГО клана:
    /// очки и место на конец каждого дня + остаток защит. Берётся прямо из API, без расчётов.
    /// </summary>
    public List<WarPeriodLog> DayLogs { get; set; } = [];

    public bool IsWarDay => PeriodType is "warDay" or "colosseum";
    public TimeSpan TimeLeft(DateTime utcNow) => DayEndsAtUtc - utcNow;

    /// <summary>
    /// Начало военной части текущей недели (UTC). Военные дни — это PeriodIndex 3..6 (чт-вс);
    /// текущий военный день длится сутки и заканчивается в <see cref="DayEndsAtUtc"/>, а первый
    /// военный день недели (четверг) стартовал (PeriodIndex-2) суток назад. Бои раньше этого
    /// времени относятся к ПРОШЛОЙ неделе и не должны попадать в журнал текущей недели.
    /// Плюс 3 часа запаса на погрешность времени сброса.
    /// </summary>
    public DateTime WarWeekStartUtc =>
        DayEndsAtUtc.AddDays(-(Math.Clamp(PeriodIndex, 3, 6) - 2)).AddHours(-3);
}

/// <summary>Итог одного дня гонки для клана — официальные данные periodLogs.</summary>
public class WarPeriodLog
{
    public int PeriodIndex { get; set; }                 // сквозной индекс дня за сезон
    public int DayIndex { get; set; }                    // 0..6 (нормализованный день недели гонки)

    /// <summary>0 = текущая неделя, 1 = прошлая и т.д. CR в periodLogs может отдавать
    /// дни прошлых недель — потребители фильтруют/подписывают по этому полю.</summary>
    public int WeekOffset { get; set; }
    public int PointsEarned { get; set; }                // очки клана за этот день
    public int ProgressEndOfDay { get; set; }            // накопленный прогресс на конец дня
    public int EndOfDayRank { get; set; }                // место клана на конец дня (1..5)
    public int NumOfDefensesRemaining { get; set; }      // сколько защит осталось у клана
    public int ProgressEarnedFromDefenses { get; set; }  // очки, полученные с защит
}

/// <summary>Агрегированное состояние одного клана в River Race (своего или соперника).</summary>
public class RaceClanStanding
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Fame { get; set; }                        // медали клана за всю неделю (накопленные, сумма участников)
    public int TodayFame { get; set; }                   // медали за бои ТОЛЬКО сегодня (periodPoints из JSON)
    public int BoatPoints { get; set; }                  // очки лодки сегодня (clan.fame из JSON)
    public int ParticipantCount { get; set; }
    public int DecksUsedToday { get; set; }              // сумма по участникам
    public int DecksUsed { get; set; }                   // сумма за неделю (включая тренировку)
    public bool IsFinished { get; set; }                 // клан уже доплыл до финиша
}

public class WarParticipant
{
    public required string PlayerTag { get; set; }
    public required string Name { get; set; }
    public int DecksUsedToday { get; set; }              // 0..4
    public int DecksUsed { get; set; }                   // суммарно за неделю (включая тренировочные дни!)

    /// <summary>
    /// Колоды, сыгранные только в военные дни. CR API в DecksUsed считает и тренировочные
    /// бои (которые славы не дают), из-за чего «слава/атака» занижается. Это поле
    /// корректируется по снапшоту первого военного дня; по умолчанию = DecksUsed.
    /// </summary>
    public int WarDecksUsed { get; set; }

    public int Fame { get; set; }
    public int RepairPoints { get; set; }                // очки ремонта лодки
    public int BoatAttacks { get; set; }                 // атаки на лодку соперника
    public WarPlayStatus Status { get; set; }
    public long? TelegramUserId { get; set; }            // подмешивается из БД

    /// <summary>Средняя слава за военную атаку. 0, если ещё не атаковал в войне.</summary>
    public double AverageFamePerAttack => WarDecksUsed > 0 ? (double)Fame / WarDecksUsed : 0;
}
