namespace ClanWarTracker.Application.DTOs;

public record GlobalTopPlayerDto(
    string PlayerTag,
    string Name,
    string ClanName,
    int TotalFame,
    int WeeksParticipated,
    int BestWeekFame,
    double AvgFamePerAttack,
    int Rank,
    bool IsMe);

public record GlobalTopDto(
    int WeeksWindow,
    int PlayersTracked,
    IReadOnlyList<GlobalTopPlayerDto> Players);
