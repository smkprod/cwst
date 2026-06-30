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
    List<PlayerStatusDto> Players,
    ClanInsightsDto? Insights,  // Pro: прогноз победы + здоровье клана (null на Free)
    List<WarLogWeekDto> WarLog, // журнал прошлых войн (места кланов и очки)
    List<WarDayLogDto> DayLogs); // официальный по-дневный лог гонки (periodLogs)

/// <summary>Итог дня гонки для нашего клана — официальные данные periodLogs из CR API.</summary>
public record WarDayLogDto(
    int DayIndex,                 // 0..6 (нормализованный день недели гонки)
    int PointsEarned,             // очки клана за день
    int EndOfDayRank,             // место клана на конец дня (1..5)
    int NumOfDefensesRemaining);  // осталось защит

/// <summary>Одна завершённая неделя из официального журнала войн клана.</summary>
public record WarLogWeekDto(
    int SeasonId,
    int SectionIndex,         // неделя внутри сезона (0..3)
    bool IsColosseum,
    List<WarLogClanDto> Standings); // отсортированы по месту

public record WarLogClanDto(
    int Rank,                 // 1..5
    string Name,
    int Fame,                 // медали клана за неделю
    int TrophyChange,         // +/- КВ-трофеи по итогам
    bool IsOurClan,
    List<WarLogPlayerDto>? Players = null);

public record WarLogPlayerDto(string Name, int Fame, int DecksUsed);

/// <summary>Один фактор здоровья клана (0..100).</summary>
public record HealthFactorDto(string Name, int Score);

/// <summary>Pro-аналитика: шанс победы в гонке и «здоровье» клана.</summary>
public record ClanInsightsDto(
    int? WinChance,              // % победы в гонке недели; null — тренировка
    int? WinChanceIfSlackersOut, // тот же %, если не доигравшие так и не сыграют
    string? TopRivalName,        // главный соперник (по прогнозу)
    int HealthScore,             // 0..100
    string HealthLabel,          // "Сильный клан" | "Стабильный" | "Нестабильный" | "Критично"
    List<HealthFactorDto> Factors);

/// <summary>Один клан в таблице гонки (наш или соперник).</summary>
public record RaceClanDto(
    string Tag,
    string Name,
    int Position,             // 1..5 (финишировавшие выше)
    int Fame,                 // медали за ВСЮ неделю (накопленные)
    int TodayFame,            // медали за бои ТОЛЬКО сегодня (periodPoints из CR API)
    int BoatPoints,           // очки лодки сегодня (clan.fame из CR API)
    int ProjectedFame,        // прогноз медалей к концу текущего дня
    double AvgFamePerAttack,  // среднее медалей за атаку (todayFame / decksUsedToday)
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
    string? Role,             // "Лидер" | "Соруководитель" | "Старейшина" | null (обычный)
    string? DnaLabel,         // Pro: архетип игрока ("Тащер 💪", "Надёжный 🛡" ...), null — мало данных/Free
    int ReliabilityScore);    // Pro: надёжность 0..100 (0 — нет данных/Free)

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
    string Trend,                 // "ahead" | "onPace" | "behind" — относительно среднего темпа
    int ProjectedDayFameLow,      // -1σ нижняя граница прогноза дня (игровой min)
    int ProjectedDayFameHigh);    // +1σ верхняя граница прогноза дня

/// <summary>Полный профиль игрока из CR API + агрегированная статистика войн.</summary>
public record PlayerProfileDto(
    string PlayerTag,
    string Name,
    int ExpLevel,
    int Trophies,
    int BestTrophies,
    int ClanWarTrophies,
    string? ClanName,
    string? ClanTag,
    string? ArenaName,
    List<PlayerCardDto> Cards,
    int WeeksPlayed,
    int TotalFame,
    double AvgFamePerAttack,
    // Поля из /players/{tag} — только реальные данные API:
    int WarDayWins,
    int BattleCount,
    int ThreeCrownWins,
    int CurrentWinLoseStreak,
    PathOfLegendDto? CurrentPathOfLegend,
    PathOfLegendDto? BestPathOfLegend,
    string? CurrentFavouriteCard,
    List<PlayerCardDto> CurrentDeck,
    string RoyaleApiUrl);

public record PlayerCardDto(string Name, int Level, int MaxLevel, string IconUrl);

public record PathOfLegendDto(int Trophies, int LeagueNumber, int Rank);

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
