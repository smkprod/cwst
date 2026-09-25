namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Аллея славы: кто лучший по всему сервису за сезон, а не внутри своего клана.
///
/// Две таблицы в одной выдаче, потому что экран один и переключатель между ними
/// мгновенный: отдавать их двумя запросами значило бы ждать при каждом нажатии
/// на то, что уже посчитано.
/// </summary>
/// <param name="SeasonId">Сезон, за который считали.</param>
/// <param name="ClansCounted">Сколько кланов попало в зачёт — чтобы место «12 из 16» читалось.</param>
public record HallOfFameDto(
    int SeasonId,
    int ClansCounted,
    List<HallPlayerDto> Players,
    List<HallClanDto> Clans);

/// <param name="Rank">Место, начиная с 1.</param>
/// <param name="BadgeKey">Выставленный значок игрока; null — не выбран или игрок не привязан.</param>
/// <param name="BadgeLevel">1 бронза, 2 серебро, 3 золото.</param>
/// <param name="IsSponsor">Спонсор — рисуется ярлыком и своим фоном.</param>
/// <param name="BackgroundKey">Фон, выбранный спонсором. null — обычный.</param>
public record HallPlayerDto(
    int Rank,
    string PlayerTag,
    string Name,
    string ClanName,
    string? ClanTag,
    int SeasonFame,
    int WeeksPlayed,
    string? BadgeKey,
    int BadgeLevel,
    bool IsSponsor,
    string? BackgroundKey);

/// <param name="SponsorCount">Сколько спонсоров в клане — клан с ними получает свои возможности.</param>
/// <param name="BackgroundKey">Фон клана на Аллее, если его выставил спонсор клана.</param>
public record HallClanDto(
    int Rank,
    int ClanId,
    string ClanTag,
    string ClanName,
    int SeasonFame,
    int WeeksPlayed,
    int SponsorCount,
    string? BackgroundKey);
