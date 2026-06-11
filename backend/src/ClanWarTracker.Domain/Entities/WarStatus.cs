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

    public bool IsWarDay => PeriodType is "warDay" or "colosseum";
    public TimeSpan TimeLeft(DateTime utcNow) => DayEndsAtUtc - utcNow;
}

/// <summary>Агрегированное состояние одного клана в River Race (своего или соперника).</summary>
public class RaceClanStanding
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Fame { get; set; }                        // слава клана за неделю
    public int PeriodPoints { get; set; }                // очки текущего дня (двигают лодку)
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
