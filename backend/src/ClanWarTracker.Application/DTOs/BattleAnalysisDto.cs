namespace ClanWarTracker.Application.DTOs;

/// <param name="AvgLeakWins">Средняя утечка эликсира в победах.</param>
/// <param name="AvgLeakLosses">Средняя утечка в поражениях.</param>
/// <param name="Games">Сколько боёв с известной утечкой.</param>
public record ElixirLeakDto(double AvgLeakWins, double AvgLeakLosses, double AvgLeakAll, int Games);

/// <summary>Своя колода: как ей играешь ты и как ей же играет топ.</summary>
/// <param name="MetaWinPercent">Процент побед такой же (или похожей) колоды у топа; null - в мете её нет.</param>
/// <param name="MetaSharedCards">Сколько карт совпадает с колодой топа (8 - та же самая).</param>
/// <param name="MetaCounters">Против каких карт эта колода проседает по боям топа.</param>
public record MyDeckDto(
    List<MetaCardDto> Cards,
    int Games,
    int Wins,
    double WinPercent,
    double? MetaWinPercent,
    int MetaSharedCards,
    int MetaGames,
    List<MetaCounterDto> MetaCounters,
    string? CopyLink);

/// <param name="Key">Для частей суток: night / morning / day / evening. Для дней недели: 0 (пн) .. 6 (вс).</param>
public record TimeSlotDto(string Key, int Games, double WinPercent);

/// <param name="AfterTwoLossesGames">Боёв, сыгранных сразу после двух поражений подряд.</param>
/// <param name="LongestLossStreak">Самая длинная серия поражений за период.</param>
public record TiltDto(
    int AfterTwoLossesGames,
    double AfterTwoLossesWinPercent,
    int AfterWinGames,
    double AfterWinWinPercent,
    int LongestLossStreak);

/// <summary>Карта соперника, против которой у тебя плохо получается.</summary>
public record ToughCardDto(MetaCardDto Card, int Games, double WinPercent, double DeltaPercent);

/// <summary>Доступ к полному разбору.</summary>
/// <param name="Unlocked">Полный разбор открыт: Плюс есть или платное выключено.</param>
/// <param name="TrialStarted">Триал выдан только что, этим самым открытием разбора.</param>
public record ReviewAccessDto(
    bool Paywall,
    bool Unlocked,
    bool Active,
    string? Until,
    string? Source,
    bool TrialStarted,
    bool TrialUsed,
    int TrialDays,
    int TrialMinBattles);

/// <summary>
/// Что спрятано за Плюсом - цифрами самого игрока. «Найдено 4 карты, против которых
/// ты проседаешь» продаёт лучше, чем абстрактное «больше аналитики».
/// </summary>
public record ReviewLockedDto(int ToughCards, int Decks, int Counters, bool Weekdays);

/// <param name="Games">Сколько боёв вошло в разбор (1 на 1, без дружеских и тренировок).</param>
/// <param name="SinceUtc">С какого боя копим. null - боёв нет.</param>
public record BattleAnalysisDto(
    string PlayerTag,
    int Games,
    int Wins,
    int Losses,
    int Draws,
    double WinPercent,
    string? SinceUtc,
    ElixirLeakDto? Elixir,
    List<MyDeckDto> Decks,
    List<TimeSlotDto> DayParts,
    List<TimeSlotDto> Weekdays,
    TiltDto? Tilt,
    List<ToughCardDto> ToughCards,
    int MetaBattles,
    ReviewAccessDto Access,
    ReviewLockedDto? Locked);
