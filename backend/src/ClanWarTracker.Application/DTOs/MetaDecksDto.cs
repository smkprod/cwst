namespace ClanWarTracker.Application.DTOs;

/// <summary>Карта в колоде меты. Evo - эволюция, иконка тогда эволюционная.</summary>
public record MetaCardDto(int CardId, string Name, string IconUrl, bool Evo, int Elixir);

/// <summary>Карта соперника, против которой колода проседает.</summary>
/// <param name="WinPercent">Процент побед колоды, когда у соперника есть эта карта.</param>
/// <param name="DeltaPercent">На сколько пунктов это ниже обычного для колоды.</param>
public record MetaCounterDto(MetaCardDto Card, double WinPercent, double DeltaPercent, int Games);

/// <param name="UsagePercent">Доля всех колод, сыгранных топом за окно.</param>
/// <param name="CycleElixir">Стоимость четырёх самых дешёвых карт - полный цикл.</param>
/// <param name="CopyLink">Ссылка, открывающая колоду в игре.</param>
public record MetaDeckRowDto(
    List<MetaCardDto> Cards,
    int Games,
    int Wins,
    int Draws,
    int Losses,
    double WinPercent,
    double UsagePercent,
    double AvgElixir,
    int CycleElixir,
    List<MetaCounterDto> Counters,
    string? CopyLink);

/// <param name="Battles">Сколько боёв топа вошло в окно.</param>
public record MetaDecksDto(
    string FromDayUtc,
    string ToDayUtc,
    int Battles,
    List<MetaDeckRowDto> Decks);
