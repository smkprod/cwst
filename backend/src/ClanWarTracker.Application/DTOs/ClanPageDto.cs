namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Страница клана — то, что открывается нажатием по клану на Аллее.
///
/// Появилась не ради красоты. Кнопка «написать клану» висела на строке списка, а
/// первые три клана в списке не лежат — они стоят на подиуме, и нажать по ним
/// было физически нечем. То есть недоступны для сообщения были ровно те кланы,
/// которым интереснее всего написать. Страница снимает это: она открывается и с
/// подиума, и из списка, и кнопка живёт на ней, а не на строке.
/// </summary>
/// <param name="Rank">Место на Аллее. 0 — клан ещё не в зачёте сезона.</param>
/// <param name="DesignKey">Оформление страницы из каталога ClanPageDesign.</param>
/// <param name="AcceptsMail">Принимает ли клан сообщения — кнопку иначе показывать незачем.</param>
/// <param name="IsMine">Клан зрителя: ему не пишут, ему настраивают.</param>
/// <param name="CanEdit">Зритель вправе менять оформление этой страницы.</param>
/// <param name="AvailableDesigns">Что зритель может выбрать. Пусто, если менять не вправе.</param>
public record ClanPageDto(
    int ClanId,
    string ClanTag,
    string ClanName,
    string? Motto,
    int Rank,
    int ClansCounted,
    int SeasonId,
    int SeasonFame,
    int WeeksPlayed,
    int MembersInSeason,
    int SponsorCount,
    string? BackgroundKey,
    string DesignKey,
    bool AcceptsMail,
    bool IsMine,
    bool CanEdit,
    IReadOnlyList<string> AvailableDesigns,
    IReadOnlyList<ClanPageMemberDto> Members,
    IReadOnlyList<PageWeekDto> Weeks);

/// <param name="HallRank">Место игрока на Аллее — то же число, что в общем списке.</param>
public record ClanPageMemberDto(
    int Rank,
    int HallRank,
    string PlayerTag,
    string Name,
    int SeasonFame,
    int WeeksPlayed,
    string? BadgeKey,
    int BadgeLevel,
    bool IsSponsor,
    string? BackgroundKey);

/// <param name="SectionIndex">Номер военной недели внутри сезона, с нуля.</param>
public record PageWeekDto(int SectionIndex, int Fame);
