namespace ClanWarTracker.Application.DTOs;

public record ClanStatusDto(
    string ClanTag,
    string ClanName,
    string PeriodType,        // training | warDay | colosseum
    int PeriodIndex,
    DateTime DayEndsAtUtc,
    int HoursLeft,
    string Plan,              // "free" | "pro"
    ClanStatsDto Stats,
    ClanForecastDto? Forecast, // null на Free-тарифе
    List<RaceClanDto> Race,   // все кланы гонки, отсортированы по месту
    List<PlayerStatusDto> Players);

/// <summary>Один клан в таблице гонки (наш или соперник).</summary>
public record RaceClanDto(
    string Tag,
    string Name,
    int Position,             // 1..5 (финишировавшие выше)
    int Fame,                 // медали за неделю
    int PeriodPoints,         // медали текущего дня
    int ProjectedFame,        // прогноз медалей к концу недели
    double AvgFamePerAttack,  // среднее медалей за военную атаку
    int DecksUsedToday,
    int MaxDecksToday,
    int WarTrophies,          // КВ-трофеи клана (0 — не удалось получить)
    bool IsOurClan,
    bool IsFinished);

public record PlayerStatusDto(
    string PlayerTag,
    string Name,
    int DecksUsedToday,
    int DecksUsed,            // суммарно за неделю (включая тренировку)
    int WarDecksUsed,         // только военные атаки
    int Fame,
    int RepairPoints,
    int BoatAttacks,
    double AvgFamePerAttack,  // среднее медалей за атаку
    int ProjectedDayFame,     // прогноз медалей к концу текущего дня
    int ProjectedWeekFame,    // прогноз медалей к концу недели
    int Rank,                 // место в клане по медалям
    string Status,            // "played" | "timeLeft" | "notPlayed"
    bool IsLinked,
    int ConsecutiveWars,      // Pro: сколько недель подряд участвовал (0 на Free)
    string? Role);            // "Лидер" | "Соруководитель" | "Старейшина" | null (обычный)

public record ClanStatsDto(
    int TotalFame,
    int TotalRepairPoints,
    int TotalDecksUsedToday,
    int TotalDecksUsedWeek,
    int MaxDecksToday,        // обычно 50*4=200
    int ActivePlayers,        // те, кто сделал хотя бы одну атаку за неделю
    int PlayersPlayed,        // 4/4 сегодня
    int PlayersNotPlayed,     // те, у кого красный статус
    double AvgFamePerAttack); // в среднем по клану

public record ClanForecastDto(
    int ProjectedDayFame,         // прогноз славы клана к концу дня
    int ProjectedWeekFame,        // прогноз славы к концу недели (4 военных дня)
    int ExpectedRemainingAttacksToday, // ожидаемые оставшиеся атаки
    int Confidence,               // 0..100 — насколько надёжен прогноз
    string Trend);                // "ahead" | "onPace" | "behind" — относительно среднего темпа

/// <summary>Детальная статистика для текущего игрока (в Mini App).</summary>
public record MyStatsDto(
    string PlayerTag,
    string Name,
    string ClanName,
    int Fame,
    int RepairPoints,
    int BoatAttacks,
    int DecksUsedToday,
    int DecksUsed,
    double AvgFamePerAttack,
    int ProjectedDayFame,
    int ProjectedWeekFame,
    int Rank,
    int ClanSize,
    int ContributionPercent,  // % личного вклада в общую славу клана
    string PerformanceLabel,  // "топ", "выше среднего", "средне", "ниже среднего"
    double ClanAvgFamePerAttack,
    MySeasonDto? Season);     // null — нет данных сезона или Free-тариф
