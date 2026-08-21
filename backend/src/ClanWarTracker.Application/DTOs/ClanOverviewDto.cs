namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Витрина чужого клана для игрока, чей клан ещё не подключён к боту.
/// Connected говорит, знает ли бот этот клан: от этого зависит, что показать —
/// «попроси главу написать /setup» или «твой клан уже с нами».
/// </summary>
public record ClanOverviewDto(
    string ClanTag,
    string? ClanName,
    bool Connected,
    int WarTrophies,
    int? MemberCount,
    string? CountryName,
    int? CountryRank,
    int? GlobalRank,
    List<RankedClanDto> CountryTop);
