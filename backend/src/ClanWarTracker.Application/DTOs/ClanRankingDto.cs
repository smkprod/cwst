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

/// <param name="Tag">
/// Нужен, чтобы строку в топе можно было открыть: имена кланов в CR не уникальны
/// и меняются, а страницу мы ищем по тегу.
/// </param>
public record RankedClanDto(
    int Rank, int PreviousRank, string Tag, string Name, int WarTrophies, int Members, bool IsOurClan);
