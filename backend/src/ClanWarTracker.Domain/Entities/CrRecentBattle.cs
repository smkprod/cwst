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

    /// <summary>
    /// Теги всей стороны, чей лог читаем, и всей стороны соперника. В 1х1 по одному,
    /// в 2х2 по два. Одного OpponentTag для опознания пары в турнире мало: там важно,
    /// что играли именно эти четверо, а не «кто-то из них с кем-то».
    /// </summary>
    public List<string> TeamTags { get; set; } = [];
    public List<string> OpponentTags { get; set; } = [];

    public List<CrDeckCard> MyDeck { get; set; } = [];
    public List<CrDeckCard> OpponentDeck { get; set; } = [];

    /// <summary>
    /// Сколько эликсира утекло у своей стороны за бой (стоял на десяти и не тратился).
    /// null — API поле не прислал: в старых режимах и не во всех типах боёв его нет.
    /// </summary>
    public double? ElixirLeaked { get; set; }

    /// <summary>Изменение кубков или рейтинга за бой. null — режим без счёта.</summary>
    public int? TrophyChange { get; set; }

    /// <summary>Правила боя (id режима игры) и его название, как их присылает API.</summary>
    public int? GameModeId { get; set; }
    public string? GameModeName { get; set; }
    public int? LeagueNumber { get; set; }

    /// <summary>Откуда колода: collection - своя; draft и прочее - выданная режимом.</summary>
    public string? DeckSelection { get; set; }
    public string? ArenaName { get; set; }

    /// <summary>Своя сторона и соперник целиком - для отчёта о матче.</summary>
    public CrBattleSide? Me { get; set; }
    public CrBattleSide? Opp { get; set; }
}
