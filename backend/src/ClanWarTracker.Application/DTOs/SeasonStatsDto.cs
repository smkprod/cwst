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

/// <summary>
/// Разбивка текущего сезона по неделям: каждая война отдельно + общий зачёт.
/// Завершённые недели — из официального журнала (пофамильно), текущая — из live-гонки.
/// </summary>
public record SeasonBreakdownDto(
    int SeasonId,
    int CurrentSectionIndex,        // идущая сейчас неделя сезона
    List<SeasonWeekDto> Weeks,      // по возрастанию SectionIndex (Война 1, Война 2, …)
    List<SeasonPlayerDto> SeasonTotal); // агрегат по всем неделям сезона

public record SeasonWeekDto(
    int SectionIndex,
    string Label,                   // "Война 1" / "Колизей"
    bool IsColosseum,
    bool IsCurrent,                 // эта неделя идёт прямо сейчас
    int ClanFame,                   // суммарные медали клана за неделю
    List<SeasonWeekPlayerDto> Players); // отсортированы по медалям

public record SeasonWeekPlayerDto(
    string PlayerTag,
    string Name,
    int Fame,
    int DecksUsed,
    int Rank);
