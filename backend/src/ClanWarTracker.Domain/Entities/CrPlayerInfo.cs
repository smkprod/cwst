namespace ClanWarTracker.Domain.Entities;

public class CrPlayerInfo
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int ExpLevel { get; set; }
    public int Trophies { get; set; }
    public int BestTrophies { get; set; }
    public int ClanWarTrophies { get; set; }
    public string? ClanTag { get; set; }
    public string? ClanName { get; set; }
    public string? ArenaName { get; set; }

    // --- Дополнительные поля из /players/{tag} (только реальные данные API) ---
    public int WarDayWins { get; set; }              // победы в военных днях за карьеру
    public int BattleCount { get; set; }             // всего боёв
    public int Wins { get; set; }                    // побед за карьеру
    public int Losses { get; set; }                  // поражений за карьеру

    /// <summary>
    /// Максимальный игровой уровень карты на данный момент (у обычных карт).
    /// Нужен, чтобы переводить уровни из API в игровые — см. CrCard.Level.
    /// </summary>
    public int MaxCardLevel { get; set; }
    public int ThreeCrownWins { get; set; }          // победы «3 короны»
    public int CurrentWinLoseStreak { get; set; }    // текущая серия (отриц. = поражения)
    public CrPathOfLegend? CurrentPathOfLegend { get; set; }
    public CrPathOfLegend? BestPathOfLegend { get; set; }
    public string? CurrentFavouriteCard { get; set; }
    public List<CrDeckCard> CurrentDeck { get; set; } = [];

    public List<CrCard> Cards { get; set; } = [];
}

/// <summary>Результат сезона Пути Легенд (Path of Legends) — прямо из API.</summary>
public class CrPathOfLegend
{
    public int Trophies { get; set; }
    public int LeagueNumber { get; set; }
    public int Rank { get; set; }                    // 0 = вне топ-листа (API не вернул место)
}

/// <summary>Карта из текущей колоды игрока.</summary>
/// <summary>
/// Карта колоды. ВНИМАНИЕ: CR API отдаёт уровень ОТНОСИТЕЛЬНО редкости — у легендарки
/// максимум приходит как level=maxLevel=8, хотя в игре это 16-й уровень. Здесь Level уже
/// переведён в игровой: level + (максимум по всем картам − maxLevel этой карты).
/// </summary>
public class CrDeckCard
{
    public required string Name { get; set; }
    public int Level { get; set; }        // игровой уровень (как в игре)
    public int MaxLevel { get; set; }     // потолок для этой карты, тоже в игровой шкале
    public string IconUrl { get; set; } = "";
}

/// <inheritdoc cref="CrDeckCard"/>
public class CrCard
{
    public required string Name { get; set; }
    public int Level { get; set; }        // игровой уровень (как в игре)
    public int MaxLevel { get; set; }     // потолок для этой карты, тоже в игровой шкале
    public string IconUrl { get; set; } = "";
}
