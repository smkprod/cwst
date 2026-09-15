namespace ClanWarTracker.Application.DTOs;

/// <summary>Один бой в карточке игрока: чем играл он, чем соперник, чем кончилось.</summary>
public record TopBattleDto(
    string BattleTimeUtc,
    string Type,
    bool Won,
    int CrownsFor,
    int CrownsAgainst,
    string? OpponentName,
    List<TopDeckCardDto> MyDeck,
    List<TopDeckCardDto> OpponentDeck);

/// <param name="Battles">
/// Последние бои из журнала игрока. API хранит около двадцати пяти — глубже
/// заглянуть нельзя, поэтому и не обещаем.
/// </param>
public record TopPlayerDetailDto(
    string PlayerTag,
    string Name,
    string? ClanName,
    int Trophies,
    int BestTrophies,
    int ExpLevel,
    int Wins,
    int Losses,
    int ThreeCrownWins,
    int? Rank,                 // место в последнем снимке; null — не в топе
    List<TopDeckCardDto> CurrentDeck,
    List<TopBattleDto> Battles);
