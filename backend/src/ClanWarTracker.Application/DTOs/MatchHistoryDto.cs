namespace ClanWarTracker.Application.DTOs;

/// <param name="Available">Трекер открыт этому человеку (закрытый тест).</param>
public record TrackerStateDto(bool Available, bool Enabled, bool DmBlocked, bool MutedToday);

/// <param name="Label">Название, уже с «колода с «X»» для колод вне архетипов.</param>
public record ArchDto(string Key, string Label, MetaCardDto? Card);

/// <param name="Verdict">Код вывода (afk, levels, close, ...) - для значка в строке.</param>
/// <param name="LossReason">Главная причина поражения (levels, matchup, tilt, leak, close, outplayed); null - не поражение.</param>
public record MatchRowDto(
    int Id,
    DateTime TimeUtc,
    string Mode,
    int Result,
    int CrownsFor,
    int CrownsAgainst,
    int? TrophyChange,
    string? OppName,
    ArchDto? Arch,
    List<MetaCardDto> KeyCards,
    string? Verdict,
    double? LevelGap,
    double? TopPct = null,
    int TopGames = 0,
    string? LossReason = null);

/// <param name="Tilt">В заходе была серия из трёх поражений и больше.</param>
public record MatchSessionDto(
    DateTime StartUtc, DateTime EndUtc, int Wins, int Losses, int Draws, int Trophies, bool Tilt, string Strip,
    List<MatchRowDto> Matches);

/// <param name="DeltaPp">Сглаженный процент против архетипа минус обычный, в процентных пунктах.</param>
public record ArchetypeRowDto(ArchDto Arch, int Games, int Wins, int Losses, double DeltaPp);

/// <param name="LossAvg">Средняя разница уровней в поражениях.</param>
/// <param name="UnderWinPct">% побед, когда соперник прокачан на уровень и больше.</param>
/// <param name="EvenWinPct">% побед при равных уровнях.</param>
public record LevelSplitDto(double? LossAvg, double? WinAvg, double? UnderWinPct, double? EvenWinPct);

/// <param name="Weekday">0 - понедельник.</param>
/// <param name="Part">0 - ночь, 1 - утро, 2 - день, 3 - вечер.</param>
public record HeatCellDto(int Weekday, int Part, int Games, int Wins);

/// <param name="Heat">«Когда ты играешь лучше»: 7 дней × 4 части суток за 30 дней.</param>
/// <param name="BasePct">Свой обычный процент побед за 30 дней - нулевая точка для цвета карты и радара.</param>
public record MatchAggregatesDto(
    List<ArchetypeRowDto> Archetypes, int ArchetypesLocked, LevelSplitDto? Levels,
    List<HeatCellDto>? Heat = null, double BasePct = 0);

/// <param name="Aggregates">Только на первой странице.</param>
/// <param name="LockedOlder">Боёв старше 7 дней, которые видны в Плюсе.</param>
/// <param name="NextBefore">Курсор следующей страницы; null - дальше пусто.</param>
public record MatchHistoryDto(
    TrackerStateDto Tracker,
    bool Unlocked,
    int HistoryDays,
    MatchAggregatesDto? Aggregates,
    List<MatchSessionDto> Sessions,
    int LockedOlder,
    DateTime? NextBefore);

/// <param name="Form">0 - обычная, 1 - эволюция, 2 - герой.</param>
/// <param name="Level">Игровой уровень; 0 - неизвестен.</param>
public record MatchCardDto(int CardId, string Name, string IconUrl, int Level, int Form, int Elixir);

/// <param name="PrincessHp">HP уцелевших принцесс-башен.</param>
public record MatchSideDto(
    List<MatchCardDto> Deck, MatchCardDto? TowerTroop, int? KingHp, List<int>? PrincessHp, double? Leak,
    int? Trophies, string? CopyLink);

public record MatchOppDto(string? Name, string? Tag, string? Clan, int? Trophies, int? Diff, int? GlobalRank);

public record MatchLevelsDto(double MyAvg, double OppAvg, double Gap, MatchCardDto? LowestMine);

public record MatchLeakDto(double? Mine, double? Theirs, double? Usual);

public record MatchReportSessionDto(DateTime StartUtc, int Index, int Count, int Wins, int Losses, int Draws, int Trophies, string Strip);

/// <param name="VsArch">«Против Golem за месяц 3–8 (обычно ты 54%)».</param>
/// <param name="BetterDeck">«Против Golem лучше идёт твоя колода с «Хогом»: 5–1».</param>
/// <param name="DeckVsArch">Эта колода против этого архетипа за месяц.</param>
public record MatchPlusDto(string? VsArch, string? BetterDeck, int? DeckVsArchWins, int? DeckVsArchLosses);

/// <param name="Hint">Строки Плюса открыты как бесплатная подсказка дня (🎁).</param>
/// <param name="LockedCount">Сколько строк Плюса скрыто.</param>
/// <param name="HasDetail">Есть подробности (башни, уровни) - у боёв до трекера их нет.</param>
/// <param name="LossReason">Главная причина поражения, см. LossReasons; null - не поражение.</param>
public record MatchReportDto(
    int Id,
    DateTime TimeUtc,
    string Mode,
    string? ModeName,
    int Result,
    int CrownsFor,
    int CrownsAgainst,
    int? TrophyChange,
    MatchOppDto Opp,
    ArchDto? Arch,
    double? OppAvgElixir,
    MatchSideDto Me,
    MatchSideDto Them,
    MatchLevelsDto? Levels,
    MatchLeakDto? Leak,
    string? VerdictCode,
    string? Verdict,
    MatchReportSessionDto Session,
    MatchPlusDto? Plus,
    bool Hint,
    int LockedCount,
    bool HasDetail,
    bool Unlocked,
    MatchupDto? Matchup = null,
    string? LossReason = null);

/// <param name="Reliability">reliable (100+ боёв), adequate (20+), low.</param>
public record MatchupTierDto(string Key, int Wins, int Draws, int Losses, int Games, double WinPercent, string Reliability);

/// <param name="Cycle">Сумма четырёх самых дешёвых карт - сколько стоит прокрутить колоду.</param>
public record DeckShapeDto(double AvgElixir, int Cycle);

/// <param name="Top">Как такие колоды играют друг против друга в топ-500 за неделю. null - боёв топа ещё нет.</param>
/// <param name="Headline">Ключ самой точной ступени с достаточной выборкой.</param>
/// <param name="Own">То же по своим боям за месяц (Плюс). null - закрыто или нечего показать.</param>
public record MatchupDto(
    List<MatchupTierDto>? Top, int TopBattles, string? Headline,
    List<MatchupTierDto>? Own, bool OwnLocked,
    DeckShapeDto? Me, DeckShapeDto? Them);
