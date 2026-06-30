namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Одна завершённая неделя River Race из официального журнала CR API
/// (/clans/{tag}/riverracelog) — итоговые места кланов и медали игроков.
/// </summary>
public class RiverRaceLogWeek
{
    public int SeasonId { get; set; }
    public int SectionIndex { get; set; }                 // неделя внутри сезона (0..4)

    /// <summary>
    /// Колизей — ПОСЛЕДНЯЯ неделя сезона. Количество военных недель в сезоне разное
    /// (бывает 3, бывает 4), поэтому нельзя завязываться на "section == 3". Признак
    /// вычисляется по контексту журнала (см. ClashRoyaleApiClient.GetRiverRaceLogAsync):
    /// неделя — колизей, если это максимальный section своего сезона и в журнале уже
    /// есть неделя более позднего сезона (т.е. сезон точно завершён).
    /// </summary>
    public bool IsColosseum { get; set; }

    /// <summary>Финальная таблица недели, отсортирована по месту (1 — победитель).</summary>
    public List<RiverRaceLogStanding> Standings { get; set; } = [];
}

/// <summary>Итог одного клана за неделю: место, изменение КВ-трофеев, медали.</summary>
public class RiverRaceLogStanding
{
    public int Rank { get; set; }                         // 1..5
    public int TrophyChange { get; set; }                 // +/- КВ-трофеи по итогам недели
    public required string ClanTag { get; set; }
    public required string ClanName { get; set; }
    public int Fame { get; set; }                         // медали клана за неделю
    public List<RiverRaceLogPlayer> Participants { get; set; } = [];
}

/// <summary>Игрок в итогах недели (включая уже ушедших из клана).</summary>
public class RiverRaceLogPlayer
{
    public required string PlayerTag { get; set; }
    public required string Name { get; set; }
    public int Fame { get; set; }
    public int DecksUsed { get; set; }
}
