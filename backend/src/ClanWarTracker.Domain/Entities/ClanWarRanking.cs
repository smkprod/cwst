namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Место клана в официальных рейтингах по КВ-трофеям (/locations/.../rankings/clanwars).
/// Ранги есть только у кланов из топ-1000 своей страны/мира — иначе null.
/// </summary>
public class ClanWarRanking
{
    public int ClanWarTrophies { get; set; }
    public string? CountryName { get; set; }            // null — у клана не указана страна
    public int? CountryRank { get; set; }
    public int? CountryPreviousRank { get; set; }
    public int? GlobalRank { get; set; }
    public int? GlobalPreviousRank { get; set; }

    /// <summary>Топ кланов страны по КВ-трофеям (для таблицы).</summary>
    public List<RankedClan> CountryTop { get; set; } = [];
}

public class RankedClan
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Rank { get; set; }
    public int PreviousRank { get; set; }
    public int WarTrophies { get; set; }
    public int Members { get; set; }
}
