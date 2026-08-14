namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Снимок состояния River Race на конкретный день войны.
/// Воркер upsert'ит его каждые 30 минут — последняя запись дня = финальный результат.
/// Ключ: (ClanId, SeasonId, SectionIndex, PeriodIndex).
/// </summary>
public class WarSnapshot
{
    public int Id { get; set; }
    public int ClanId { get; set; }
    public Clan? Clan { get; set; }

    public int SeasonId { get; set; }            // сезон CR
    public int SectionIndex { get; set; }        // неделя внутри сезона
    public int PeriodIndex { get; set; }         // 0..6 (3..6 — военные дни)
    public required string PeriodType { get; set; } // "warDay" | "colosseum"

    /// <summary>
    /// Откуда данные: "live" — снято с текущей войны (может быть неполным, если день
    /// закончился между тиками), "log" — из официального журнала (/riverracelog), т.е.
    /// подтверждённый финал недели. Агрегаты предпочитают "log" — это гарантия качества.
    /// </summary>
    public string Source { get; set; } = "live";

    public DateTime CapturedAtUtc { get; set; }  // время последнего обновления
    public int TotalFame { get; set; }           // слава клана (накопительная за неделю)
    public int TotalDecksUsed { get; set; }      // колоды за неделю
    public int ParticipantCount { get; set; }

    public List<PlayerWarSnapshot> Players { get; set; } = [];
}

/// <summary>Результат конкретного игрока в снимке.</summary>
public class PlayerWarSnapshot
{
    public int Id { get; set; }
    public int WarSnapshotId { get; set; }
    public WarSnapshot? Snapshot { get; set; }

    public required string PlayerTag { get; set; }
    public required string Name { get; set; }
    public int Fame { get; set; }                // накопительная слава за неделю
    public int DecksUsed { get; set; }
    public int DecksUsedToday { get; set; }
    public int BoatAttacks { get; set; }
    public int RepairPoints { get; set; }
}
