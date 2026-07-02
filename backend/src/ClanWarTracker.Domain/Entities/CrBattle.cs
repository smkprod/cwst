namespace ClanWarTracker.Domain.Entities;

/// <summary>Один военный бой из боевого лога игрока (/players/{tag}/battlelog).</summary>
public class CrBattle
{
    public DateTime BattleTimeUtc { get; set; }
    public required string PlayerTag { get; set; }
    public required string PlayerName { get; set; }
    public bool Won { get; set; }
    public int CrownsFor { get; set; }
    public int CrownsAgainst { get; set; }
    public string? OpponentName { get; set; }
    public string Type { get; set; } = "";
}
