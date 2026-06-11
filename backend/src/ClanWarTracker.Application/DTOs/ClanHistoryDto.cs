namespace ClanWarTracker.Application.DTOs;

public record ClanHistoryDto(List<WeekHistoryDto> Weeks);

public record WeekHistoryDto(
    int SeasonId,
    int SectionIndex,
    bool IsColosseum,
    int FinalFame,            // слава клана на конец недели (последний снимок)
    int? MyFame,              // слава текущего игрока за неделю (если запрошено)
    List<DayHistoryDto> Days,
    List<TopPlayerDto> TopPlayers);

public record DayHistoryDto(
    int PeriodIndex,
    int DayNumber,            // 1..4
    DateTime CapturedAtUtc,
    int TotalFame,            // накопительная слава на конец дня
    int DayFame);             // прирост за день

public record TopPlayerDto(string PlayerTag, string Name, int Fame);
