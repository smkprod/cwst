namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Бой из журнала игрока — со всем, что отдаёт API, а не только исход.
///
/// Существующий CrBattle оставлен как есть: он про клановую войну и сознательно
/// отфильтрован по её типам. Здесь нужен другой срез — последние бои любого режима
/// вместе с колодами обеих сторон, потому что смотреть на топ-игрока без его
/// колоды бессмысленно.
/// </summary>
public class CrRecentBattle
{
    public DateTime BattleTimeUtc { get; set; }

    /// <summary>Режим как его называет API: PvP, riverRacePvP, pathOfLegend и т.д.</summary>
    public string Type { get; set; } = "";

    public bool Won { get; set; }
    public int CrownsFor { get; set; }
    public int CrownsAgainst { get; set; }

    public string? OpponentName { get; set; }
    public string? OpponentTag { get; set; }

    public List<CrDeckCard> MyDeck { get; set; } = [];
    public List<CrDeckCard> OpponentDeck { get; set; } = [];
}
