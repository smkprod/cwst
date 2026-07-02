namespace ClanWarTracker.Application.DTOs;

/// <summary>Место клана в официальных рейтингах по КВ-трофеям (страна + мир, только топ-1000).</summary>
public record ClanRankingDto(
    int WarTrophies,
    string? CountryName,
    int? CountryRank,
    int? CountryPreviousRank,
    int? GlobalRank,
    int? GlobalPreviousRank,
    List<RankedClanDto> CountryTop);

public record RankedClanDto(int Rank, int PreviousRank, string Name, int WarTrophies, int Members, bool IsOurClan);
