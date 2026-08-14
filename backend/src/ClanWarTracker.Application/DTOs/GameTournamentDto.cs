namespace ClanWarTracker.Application.DTOs;

/// <summary>Отслеживаемый игровой турнир + живые данные из CR API (null, если не отдались).</summary>
public record GameTournamentDto(
    int Id,
    string TournamentTag,
    string? Password,
    string CreatorName,
    bool IsCreator,                 // текущий пользователь может удалить запись
    GameTournamentLiveDto? Live);

/// <summary>Живые данные турнира — только то, что вернул /tournaments/{tag}.</summary>
public record GameTournamentLiveDto(
    string Name,
    string? Description,
    string Status,                  // IN_PREPARATION | IN_PROGRESS | ENDED | UNKNOWN
    int Capacity,
    int MaxCapacity,
    int LevelCap,
    int FirstPlaceCardPrize,
    string? GameMode,
    int? StartsInSeconds,           // до старта (если в подготовке)
    int? EndsInSeconds,             // до конца (если идёт)
    List<GameTournamentMemberDto> Members);

public record GameTournamentMemberDto(int Rank, string Name, int Score, string? ClanName);
