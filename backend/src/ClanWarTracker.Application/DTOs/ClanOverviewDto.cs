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
    List<RankedClanDto> CountryTop,
    // Профиль клана из /clans/{tag} — то, ради чего клан ищут в поиске
    string? Description = null,
    string? Type = null,            // open | inviteOnly | closed
    int ClanScore = 0,
    int RequiredTrophies = 0,
    int DonationsPerWeek = 0,
    List<ClanMemberDto>? Members = null);

/// <summary>Участник клана в витрине поиска.</summary>
public record ClanMemberDto(
    string PlayerTag,
    string Name,
    string Role,        // leader | coLeader | elder | member
    int Trophies,
    int Donations);
