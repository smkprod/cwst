namespace ClanWarTracker.Domain.Entities;

/// <summary>Строка мирового рейтинга: то, что отдаёт сам rankings, без похода в профиль.</summary>
public class CrRankedPlayer
{
    public int Rank { get; set; }
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Trophies { get; set; }
    public int ExpLevel { get; set; }
    public string? ClanName { get; set; }
}
