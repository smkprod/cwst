namespace ClanWarTracker.Application.DTOs;

/// <summary>Журнал военных боёв клана за текущую неделю: счёт побед/поражений + список боёв.</summary>
public record WarJournalDto(
    int Won,
    int Lost,
    int Total,
    List<WarBattleDto> Battles);   // новые первыми

public record WarBattleDto(
    string PlayerName,
    string PlayerTag,
    DateTime BattleTimeUtc,
    bool Won,
    int CrownsFor,
    int CrownsAgainst);
