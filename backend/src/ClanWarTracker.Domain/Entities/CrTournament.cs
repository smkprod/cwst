namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Живые данные игрового турнира Clash Royale из /tournaments/{tag}.
/// Только то, что реально отдаёт API — ничего не вычисляем «от себя».
/// </summary>
public class CrTournament
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "UNKNOWN"; // IN_PREPARATION | IN_PROGRESS | ENDED | UNKNOWN
    public int Capacity { get; set; }                // сколько игроков уже зашло
    public int MaxCapacity { get; set; }             // вместимость
    public int LevelCap { get; set; }                // ограничение по уровню карт
    public int FirstPlaceCardPrize { get; set; }     // приз за 1 место (карты)
    public string? GameMode { get; set; }
    public DateTime? CreatedTime { get; set; }
    public DateTime? StartedTime { get; set; }
    public DateTime? EndedTime { get; set; }
    public int PreparationDuration { get; set; }     // сек до старта от createdTime
    public int Duration { get; set; }                // сек длительность активной фазы
    public List<CrTournamentMember> Members { get; set; } = [];
}

/// <summary>Участник игрового турнира (таблица результатов).</summary>
public class CrTournamentMember
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Rank { get; set; }
    public int PreviousRank { get; set; }
    public int Score { get; set; }
    public string? ClanName { get; set; }
}
