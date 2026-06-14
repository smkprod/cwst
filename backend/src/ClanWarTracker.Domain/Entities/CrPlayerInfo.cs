namespace ClanWarTracker.Domain.Entities;

public class CrPlayerInfo
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int ExpLevel { get; set; }
    public int Trophies { get; set; }
    public int ClanWarTrophies { get; set; }
    public string? ClanTag { get; set; }
    public string? ClanName { get; set; }
    public string? ArenaName { get; set; }
    public List<CrCard> Cards { get; set; } = [];
}

public class CrCard
{
    public required string Name { get; set; }
    public int Level { get; set; }
    public int MaxLevel { get; set; }
    public string IconUrl { get; set; } = "";
}
