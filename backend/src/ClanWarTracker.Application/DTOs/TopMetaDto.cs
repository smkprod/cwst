namespace ClanWarTracker.Application.DTOs;

/// <summary>Одна карта в витрине популярности топа.</summary>
/// <param name="Percent">Доля колод топа, где карта встречается.</param>
/// <param name="DeltaPercent">
/// Изменение доли за неделю в процентных пунктах. null — сравнивать не с чем:
/// снимков меньше чем на неделю, и показывать «+0» было бы враньём.
/// </param>
public record TopCardDto(int CardId, string Name, string IconUrl, double Percent, double? DeltaPercent);

/// <param name="ComparedToDayUtc">С каким днём сравнивали; null — не с чем.</param>
/// <param name="PlayersWithDeck">
/// У скольких из топа колода вообще известна: закрытые профили API не отдаёт,
/// и проценты считаются от этого числа, а не от всей тысячи.
/// </param>
/// <param name="CutoffTrophies">Кубки последнего места — порог входа в топ.</param>
public record TopMetaDto(
    string DayUtc,
    string? ComparedToDayUtc,
    int PlayersTracked,
    int PlayersWithDeck,
    int CutoffTrophies,
    int TopTrophies,
    double AvgLevel,
    List<TopCardDto> Cards);

/// <summary>Строка списка топа.</summary>
public record TopPlayerRowDto(
    int Rank,
    string PlayerTag,
    string Name,
    string? ClanName,
    int Trophies,
    int ExpLevel,
    List<TopDeckCardDto> Deck);

public record TopDeckCardDto(int CardId, string Name, string IconUrl);
