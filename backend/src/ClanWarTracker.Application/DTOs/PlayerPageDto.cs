namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Страница игрока с Аллеи славы.
///
/// Отличается от собственной вкладки «Я» тем, что открывается на чужого: здесь
/// нет ни советов, ни прогнозов, ни того, чего посторонним знать незачем. Только
/// витрина — место, медали, значки и оформление. Ровно то, ради чего покупают
/// спонсорство: страница, которую можно показать.
/// </summary>
/// <param name="Rank">Место на Аллее за сезон.</param>
/// <param name="ClanId">Клан игрока, если он есть в боте — чтобы с его страницы уйти на клан.</param>
/// <param name="Badges">Все выбитые значки, а не только выставленный напоказ.</param>
/// <param name="ShowcaseKey">Значок, который игрок поставил рядом с именем.</param>
public record PlayerPageDto(
    int Rank,
    int TotalPlayers,
    int SeasonId,
    string PlayerTag,
    string Name,
    string ClanName,
    string? ClanTag,
    int? ClanId,
    int SeasonFame,
    int WeeksPlayed,
    int BestWeekFame,
    bool IsSponsor,
    string? BackgroundKey,
    string? ShowcaseKey,
    int ShowcaseLevel,
    bool IsMe,
    IReadOnlyList<PlayerPageBadgeDto> Badges,
    IReadOnlyList<PageWeekDto> Weeks);

/// <param name="Level">1 бронза, 2 серебро, 3 золото.</param>
public record PlayerPageBadgeDto(string Key, int Level, int Value);
