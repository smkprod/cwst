namespace ClanWarTracker.Application.DTOs;

/// <summary>Сезонный зачёт клана: кто сколько набил славы в КВ за сезон (~месяц, 3-5 недель).</summary>
public record SeasonStatsDto(
    int SeasonId,
    int WeeksTracked,         // по скольким неделям есть данные
    List<SeasonPlayerDto> Players);

public record SeasonPlayerDto(
    string PlayerTag,
    string Name,
    int TotalFame,            // сумма финальной славы по неделям сезона
    int WeeksParticipated,    // в скольких неделях участвовал (слава > 0)
    int BestWeekFame,         // лучшая неделя
    int Rank);

/// <summary>Сезонная сводка для конкретного игрока (вкладка «Я»).</summary>
public record MySeasonDto(
    int SeasonId,
    int TotalFame,
    int Rank,
    int ClanSize,             // сколько игроков в сезонном зачёте
    int WeeksParticipated,
    int BestWeekFame,
    int WeeksTracked);
