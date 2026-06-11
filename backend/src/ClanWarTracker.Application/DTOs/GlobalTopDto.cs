namespace ClanWarTracker.Application.DTOs;

/// <summary>Игрок в глобальном топе бота (среди привязавших аккаунт, по всем кланам).</summary>
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

public record GlobalTopDto(int WeeksWindow, int PlayersTracked, List<GlobalTopPlayerDto> Players);
